// PURPOSE: The ANIMATION LAB (F3) - a catalogue of every animation in the game, each
// playable on demand, with knobs for the conditions that modulate them (combo streak, sweep
// count, overtime level, board darkness...). Built for retiming and reworking animations
// without having to reach the game state that normally triggers them.
//
// THE ONE RULE THIS FILE FOLLOWS: it drives the REAL animation code, never a copy. Every
// entry calls the same method the game calls (CardLayerView.PlayDebugAnimation,
// BoardView.PlayWaterAnimation, ShakeForBlast, FloatingTextFx.Spawn...) and only fabricates
// the ARGUMENTS - synthetic water frames, a scratch card, a generated mine path. A lab that
// re-implemented the animations would agree with the game exactly until the day one changed.
//
// It touches no game rules: everything here is presentation, so nothing it fires can alter a
// run. Two consequences worth knowing:
//  - TurnReport cannot be fabricated (its setters are internal, by design), so the composite
//    turn feedback is reassembled from its own parts rather than replayed from a fake report;
//  - what the lab paints STAYS until it is cleared. There is no automatic resync after each
//    play, deliberately: a marker you fired should still be there while you look at it. The
//    RESET entry and closing the lab both put the screen back in sync with Core.

using System.Collections;
using System.Collections.Generic;
using ProjectBlock.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectBlock.View
{
    partial class GameUiController
    {
        private AnimationLabView animLab;

        /// <summary>The catalogue, rebuilt on every open so a language switch re-texts it.</summary>
        private readonly List<AnimationLabView.Row> animRows = new List<AnimationLabView.Row>();

        /// <summary>What each catalogue row does. Parallel to animRows; null on a header.</summary>
        private readonly List<System.Action> animActions = new List<System.Action>();

        private int animSelected;
        private int animScroll;
        private System.Action animLastPlayed;
        private string animLastLabel = string.Empty;
        private float animLoopTimer;

        /// <summary>Set while the lab has changed presentation state the game would otherwise
        /// own (darkness, the retro skin). Re-applied after anything that resyncs.</summary>
        private bool animDark;
        private bool animRetro;
        private bool animLoop;

        // ---- condition knobs: index into the tables below ----
        private int animCombo = 3;
        private int animSweeps = 1;
        private int animOvertime = 1;
        private int animCellsIndex = 3;
        private int animElementIndex;
        private int animInfectIndex = 2;
        private int animSpeedIndex = 3;

        /// <summary>Which way the gravity entry points next (see AnimGravityField).</summary>
        private int animGravityStep;

        /// <summary>
        /// RIGHT, UP, LEFT, DOWN - in that order on purpose. The lab starts (and RESET leaves it)
        /// under ordinary gravity, so pressing the entry four times walks exactly the four
        /// transitions worth watching: DOWN -> RIGHT (the power being used), RIGHT -> UP and
        /// UP -> LEFT (being re-aimed, which is the hardest one to get right), and LEFT -> DOWN
        /// (the field collapsing back to an ordinary board).
        /// </summary>
        private static readonly GridPos[] AnimGravityFlows =
        {
            new GridPos(1, 0), new GridPos(0, 1), new GridPos(-1, 0), new GridPos(0, -1)
        };

        private static readonly int[] AnimCellCounts = { 1, 2, 3, 5, 8, 12, 20, 40 };
        private static readonly float[] AnimSpeeds = { 0.1f, 0.25f, 0.5f, 1f, 2f };
        private static readonly int[] AnimInfectPercents = { 0, 25, 50, 75, 100 };

        private static readonly BlockElement[] AnimElements =
        {
            BlockElement.Fire, BlockElement.Water, BlockElement.Gold, BlockElement.Obsidian,
            BlockElement.Dynamite, BlockElement.Targeted, BlockElement.Ghost,
            BlockElement.Negative, BlockElement.Mechanical, BlockElement.Fox,
            BlockElement.Transparent
        };

        private const float AnimLoopInterval = 1.4f;
        private const float AnimHoldSeconds = 1.6f;

        private bool AnimLabOpen
        {
            get { return animLab != null && animLab.IsOpen; }
        }

        // ------------------------------------------------------------------ open / close

        private void OpenAnimationLab()
        {
            if (session == null)
            {
                return;
            }
            BuildAnimCatalogue();
            animSelected = FirstPlayableRow(0, +1);
            animScroll = 0;
            animLoopTimer = 0f;
            animLastPlayed = null;
            animLastLabel = string.Empty;
            HideTooltip();
            // The joker strip is anchored to the TOP-RIGHT of the HUD canvas, which is screen
            // space and therefore draws over this panel whatever its sorting order. It is put
            // away while the lab is open; the two entries that animate it bring it back (see
            // PlayAnimationRow). The power strip is on the left and needs no such care.
            jokerBar.SetVisible(false);
            RedrawAnimationLab();
        }

        private void CloseAnimationLab()
        {
            animLab.Hide();
            // Every knob that reaches outside the panel is put back: a lab left at 0.25x or with
            // the arena dark would look like a bug the moment it was forgotten about.
            Time.timeScale = 1f;
            animSpeedIndex = 3;
            animLoop = false;
            animDark = false;
            animRetro = false;
            jokerBar.SetVisible(true);
            StopAnimFallSequence();
            StopAnimBurstSequence();
            // AnimResync below puts the real board back up if a boss scene was showing its own.
            StopAnimBossLift();
            StopAnimRot();
            StopAnimRaw();
            StopAnimSnake();
            GangreneView.Layers.Defaults();
            SnakeView.Layers.Defaults();
            SnakeVfxController.Layers.AllOn();
            SnakeEatView.Layers.Defaults();
            SnakeDefeatView.Layers.Defaults();
            // The raw entries reach outside the board: the overtime look is driven from the round
            // by the resync below, so it is wound back to nothing first.
            overtimePressure.Stop();
            overtimeVignette.SetActive(false);
            boardView.SetOvertimeGlow(0);
            MatryoshkaView.Layers.AllOn();
            PhaseFoldView.Layers.AllOn();
            CryoSublimationView.Layers.AllOn();
            MomentumPeelView.Layers.AllOn();
            // Landed at once, before the real board comes back: its cells belong to the lab's board.
            bossMove.Stop();
            BossMoveView.Layers.Defaults();
            AnimResync();
        }

        /// <summary>Puts the screen back in step with Core - every marker, the darkness and the
        /// retro skin come from real state again - and then re-applies the lab's own overrides
        /// on top, so a knob that reads ON is still ON afterwards.</summary>
        private void AnimResync()
        {
            if (session == null || session.CurrentRound == null)
            {
                return;
            }
            StopAnimPress();
            StopAnimHost();
            StopAnimMapus();
            boardView.ClearPreview();
            RefreshAll(null);
            SyncRetroPresentation();
            AnimApplyDarkness();
            ApplyAnimRetroSkin();
        }

        // ------------------------------------------------------------------ input

        /// <summary>
        /// Owns the whole frame while the lab is open. Deliberately called BEFORE the
        /// water/sweeper input lock in Update: the lab fires exactly those animations, and a
        /// panel that stopped taking clicks while its own animation played would be unusable.
        /// </summary>
        private void HandleAnimationLabInput(Keyboard kb, Mouse mouse)
        {
            if (kb != null && (kb.escapeKey.wasPressedThisFrame || kb.f3Key.wasPressedThisFrame))
            {
                CloseAnimationLab();
                return;
            }
            if (kb != null && kb.spaceKey.wasPressedThisFrame)
            {
                ReplayLastAnimation();
                return;
            }
            if (kb != null && (kb.downArrowKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame))
            {
                int step = kb.downArrowKey.wasPressedThisFrame ? +1 : -1;
                animSelected = FirstPlayableRow(animSelected + step, step);
                ScrollSelectionIntoView();
                RedrawAnimationLab();
                return;
            }
            if (kb != null && kb.enterKey.wasPressedThisFrame)
            {
                PlayAnimationRow(animSelected);
                return;
            }
            if (mouse != null)
            {
                float wheel = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(wheel) > 0.01f)
                {
                    ScrollAnimationLab(wheel > 0f ? -3 : +3);
                    return;
                }
            }
            if (mouse != null
                && (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame))
            {
                Vector2 world = cam.ScreenToWorldPoint(mouse.position.ReadValue());
                int knob = animLab.KnobAt(world);
                if (knob >= 0)
                {
                    CycleAnimKnob(knob, mouse.rightButton.wasPressedThisFrame ? -1 : +1);
                    RedrawAnimationLab();
                    return;
                }
                int row = animLab.RowAt(world);
                if (row >= 0 && animActions[row] != null)
                {
                    animSelected = row;
                    PlayAnimationRow(row);
                    return;
                }
                // A click on the board behind the panel is not a request to close - the lab is
                // a tool you keep open while you watch. Only Esc/F3 closes it.
            }
            TickAnimationLoop();
        }

        private void TickAnimationLoop()
        {
            if (!animLoop || animLastPlayed == null)
            {
                return;
            }
            animLoopTimer += Time.deltaTime;
            if (animLoopTimer >= AnimLoopInterval)
            {
                animLoopTimer = 0f;
                animLastPlayed();
            }
        }

        private void ReplayLastAnimation()
        {
            if (animLastPlayed != null)
            {
                animLoopTimer = 0f;
                animLastPlayed();
            }
        }

        private void PlayAnimationRow(int index)
        {
            if (index < 0 || index >= animActions.Count || animActions[index] == null)
            {
                return;
            }
            animLastPlayed = animActions[index];
            animLastLabel = animRows[index].Label;
            animLoopTimer = 0f;
            // Back behind the panel before every play; the two joker-strip entries are the only
            // ones that ask for it again, so it is on screen exactly while it is the subject.
            jokerBar.SetVisible(false);
            animLastPlayed();
            RedrawAnimationLab();
        }

        private void ScrollAnimationLab(int delta)
        {
            int maxScroll = Mathf.Max(0, animRows.Count - AnimationLabView.VisibleRows);
            animScroll = Mathf.Clamp(animScroll + delta, 0, maxScroll);
            RedrawAnimationLab();
        }

        private void ScrollSelectionIntoView()
        {
            if (animSelected < animScroll)
            {
                animScroll = Mathf.Max(0, animSelected - 1);
            }
            else if (animSelected >= animScroll + AnimationLabView.VisibleRows)
            {
                animScroll = animSelected - AnimationLabView.VisibleRows + 2;
            }
            int maxScroll = Mathf.Max(0, animRows.Count - AnimationLabView.VisibleRows);
            animScroll = Mathf.Clamp(animScroll, 0, maxScroll);
        }

        /// <summary>The nearest playable row from <paramref name="from"/>, walking in
        /// <paramref name="step"/>'s direction and wrapping. Headers are skipped.</summary>
        private int FirstPlayableRow(int from, int step)
        {
            if (animRows.Count == 0)
            {
                return 0;
            }
            int index = ((from % animRows.Count) + animRows.Count) % animRows.Count;
            for (int guard = 0; guard < animRows.Count; guard++)
            {
                if (animActions[index] != null)
                {
                    return index;
                }
                index = ((index + step) % animRows.Count + animRows.Count) % animRows.Count;
            }
            return 0;
        }

        // ------------------------------------------------------------------ knobs

        private void CycleAnimKnob(int knob, int step)
        {
            switch (knob)
            {
                case 0: animCombo = WrapValue(animCombo + step, 0, 6); break;
                case 1: animSweeps = WrapValue(animSweeps + step, 1, 9); break;
                case 2:
                    animOvertime = WrapValue(animOvertime + step, 0, FlameStreakView.MaxLevel);
                    break;
                case 3: animCellsIndex = WrapIndex(animCellsIndex + step, AnimCellCounts.Length); break;
                case 4: animElementIndex = WrapIndex(animElementIndex + step, AnimElements.Length); break;
                case 5: animInfectIndex = WrapIndex(animInfectIndex + step, AnimInfectPercents.Length); break;
                case 6:
                    animSpeedIndex = WrapIndex(animSpeedIndex + step, AnimSpeeds.Length);
                    Time.timeScale = AnimSpeeds[animSpeedIndex];
                    break;
                case 7:
                    animLoop = !animLoop;
                    animLoopTimer = 0f;
                    break;
                case 8:
                    animDark = !animDark;
                    AnimApplyDarkness();
                    break;
                case 9:
                    animRetro = !animRetro;
                    ApplyAnimRetroSkin();
                    break;
            }
        }

        // Both overrides below are ADDITIVE - they can switch their effect on, never off. A round
        // that is genuinely dark ("Alacakaranlık") or genuinely in retro must not be lit up or
        // un-skinned by a lab knob being turned back to off.

        private void AnimApplyDarkness()
        {
            RoundEngine round = session != null ? session.CurrentRound : null;
            boardView.SetDarkness(animDark || (round != null && round.BoardIsDark));
        }

        /// <summary>The retro presentation without touching RoundRules.RetroMode - the lab shows
        /// the SKIN, it does not put the game into retro. Mirrors SyncRetroPresentation.</summary>
        private void ApplyAnimRetroSkin()
        {
            bool on = animRetro
                || (session != null && session.Config.Rules.RetroMode);
            if (crt != null)
            {
                crt.SetVisible(on);
            }
            if (sfx != null)
            {
                sfx.SetRetro(on);
            }
            if (bitCrush != null)
            {
                bitCrush.Active = on;
            }
            Shader.SetGlobalFloat(CrtBendId, on ? 1f : 0f);
        }

        private static int WrapValue(int value, int min, int max)
        {
            if (value > max) return min;
            if (value < min) return max;
            return value;
        }

        private static int WrapIndex(int index, int count)
        {
            return ((index % count) + count) % count;
        }

        private void RedrawAnimationLab()
        {
            var knobs = new List<AnimationLabView.Knob>
            {
                new AnimationLabView.Knob("combo streak", "kombo serisi", ComboKnobLabel()),
                new AnimationLabView.Knob("sweep count", "temizlik sayısı", animSweeps.ToString()),
                new AnimationLabView.Knob("overtime level", "uzatma seviyesi", animOvertime.ToString()),
                new AnimationLabView.Knob("cells", "hücre", AnimCellCounts[animCellsIndex].ToString()),
                new AnimationLabView.Knob("element", "element",
                    ViewUtil.ElementLabel(AnimElements[animElementIndex])),
                new AnimationLabView.Knob("infection", "enfeksiyon",
                    AnimInfectPercents[animInfectIndex] + "%"),
                new AnimationLabView.Knob("speed", "hız",
                    AnimSpeeds[animSpeedIndex].ToString("0.00") + "x"),
                new AnimationLabView.Knob("loop", "döngü", OnOff(animLoop)),
                new AnimationLabView.Knob("dark board", "karanlık alan", OnOff(animDark)),
                new AnimationLabView.Knob("retro skin", "retro görünüm", OnOff(animRetro))
            };
            string status = animLastLabel.Length > 0
                ? Loc.Pick("last: ", "son: ") + animLastLabel
                : Loc.Pick("pick an animation", "bir animasyon seç");
            animLab.SetContent(animRows, animSelected, animScroll, knobs, status);
        }

        private static string OnOff(bool on)
        {
            return on ? Loc.Pick("ON", "AÇIK") : Loc.Pick("off", "kapalı");
        }

        // ------------------------------------------------------------------ the catalogue

        private void AddAnimHeader(string en, string tr)
        {
            animRows.Add(AnimationLabView.Row.Header(en, tr));
            animActions.Add(null);
        }

        private void AddAnim(string en, string tr, System.Action play)
        {
            animRows.Add(AnimationLabView.Row.Item(en, tr));
            animActions.Add(play);
        }

        /// <summary>Every animation in the game, grouped. Adding one here is the whole of the
        /// work of making a new animation testable.
        /// EXTENSION POINT: new animations belong in the group they play in.</summary>
        private void BuildAnimCatalogue()
        {
            animRows.Clear();
            animActions.Clear();

            AddAnimHeader("state", "durum");
            AddAnim("RESET - resync to the real game", "SIFIRLA - oyuna geri dön", AnimResync);

            AddAnimHeader("cards", "kartlar");
            AddAnim("round start: shuffle + deal", "raunt başı: karma + dağıtma",
                delegate { sfx.Shuffle(); cardLayer.AnimateRoundStart(session.CurrentRound); });
            AddAnim("redraw hand", "eli yenile",
                delegate { sfx.Shuffle(); cardLayer.AnimateRedraw(session.CurrentRound); });
            AddAnim("replace one card (İade)", "tek kart değiştir (İade)", AnimReplaceCard);
            AddAnim("shuffle flourish (self)", "karma gösterisi (kendi)",
                delegate { AnimCards(CardLayerView.DebugAnim.ShuffleSelf); });
            AddAnim("shuffle from discard", "ıskartadan karma",
                delegate { sfx.Shuffle(); AnimCards(CardLayerView.DebugAnim.ShuffleFromDiscard); });
            AddAnim("pile pulse: draw", "deste nabzı: çekme",
                delegate { AnimCards(CardLayerView.DebugAnim.PilePulseDraw); });
            AddAnim("pile pulse: discard", "deste nabzı: ıskarta",
                delegate { AnimCards(CardLayerView.DebugAnim.PilePulseDiscard); });
            AddAnim("deal one card", "tek kart dağıt",
                delegate { AnimCards(CardLayerView.DebugAnim.DealOne); });
            AddAnim("discard one card", "tek kart ıskarta",
                delegate { AnimCards(CardLayerView.DebugAnim.DiscardOne); });
            AddAnim("burn a card (draw -> discard)", "kart yak (çekme -> ıskarta)",
                delegate { AnimCards(CardLayerView.DebugAnim.BurnOne); });
            AddAnim("bonus card vanishes", "bonus kart yok olur",
                delegate { sfx.Vanish(); AnimCards(CardLayerView.DebugAnim.VanishOne); });
            AddAnim("reveal beat (Şaşırtmaca)", "açılış anı (Şaşırtmaca)", AnimRevealBeat);
            AddAnim("pet demands in (Tamagotchi)", "evcil istekleri (Tamagotchi)",
                delegate { cardLayer.ShowPetDemands(AnimDemandShapes()); });
            AddAnim("pet demands out", "evcil istekleri kalksın",
                delegate { cardLayer.ShowPetDemands(null); });

            AddAnimHeader("board", "oyun alanı");
            AddAnim("water fall", "su akışı", AnimWaterFall);
            AddAnim("placement preview: valid", "yerleşim önizleme: geçerli",
                delegate { AnimPreview(true); });
            AddAnim("placement preview: invalid", "yerleşim önizleme: geçersiz",
                delegate { AnimPreview(false); });
            AddAnim("power preview cells", "güç önizleme hücreleri",
                delegate { AnimHoldPreview(delegate { boardView.ShowPowerPreview(AnimCells()); }); });
            AddAnim("retro falling piece", "retro düşen parça", AnimFallingPiece);
            AddAnim("light up around a blast (dark)", "patlama ışığı (karanlık)",
                delegate { boardView.LightUpAround(AnimCells()); });
            AddAnim("infection cores (Enfeksiyon)", "enfeksiyon çekirdekleri (Enfeksiyon)",
                AnimInfectionPips);
            AddAnim("circuit trace + blocks (Devre)", "devre izi + bloklar (Devre)",
                delegate
                {
                    // Cable AND cubes, parked together. The break starts from exactly this
                    // picture, so this is the one to look at before setting it off.
                    IReadOnlyList<GridPos> path = AnimCircuitPath();
                    boardView.ShowCircuit(path);
                    boardView.HoldCircuitBlocks(AnimCircuitCubes(path));
                });
            AddAnim("circuit BREAKS (cubes + cable)", "devre KIRILDI (bloklar + kablo)",
                AnimCircuitBreak);
            AddAnim("quarantine RELAID (Karantina)", "karantina TAŞINDI (Karantina)",
                AnimQuarantineSeal);
            AddAnim("creature nest + FEED (Besleme)", "yaratık yuvası + BESLEME (Besleme)",
                delegate
                {
                    // The nest, and then a feed in it - pressing again feeds it again, which is
                    // the only way to see the reaction the game gives it when a cube breaks there.
                    IReadOnlyList<GridPos> region = AnimCells();
                    AnimTintMarker(delegate { boardView.ShowCreature(region); });
                    if (region.Count > 0)
                    {
                        boardView.PlayCreatureFeed(region[Random.Range(0, region.Count)]);
                    }
                });
            AddAnim("Matruşka: first doll arrives", "Matruşka: ilk bebek gelir", AnimDollArrives);
            AddAnim("Matruşka: idle, every generation", "Matruşka: bekleme, tüm nesiller", AnimDollIdle);
            AddAnim("Matruşka: large -> 2 medium", "Matruşka: büyük -> 2 orta",
                delegate { AnimDollSplit(1); });
            AddAnim("Matruşka: medium -> 2 small", "Matruşka: orta -> 2 küçük",
                delegate { AnimDollSplit(2); });
            AddAnim("Matruşka: small -> 2 tiny", "Matruşka: küçük -> 2 minik",
                delegate { AnimDollSplit(3); });
            AddAnim("Matruşka: tiny opens empty", "Matruşka: minik boş açılır", AnimDollEmptied);
            AddAnim("Matruşka: three split at once", "Matruşka: aynı anda üç bölünme",
                AnimDollManySplits);
            AddAnim("Matruşka: water carries a doll", "Matruşka: su bebeği taşır", AnimDollCarried);
            AddAnim("Matruşka: last doll - boss beaten", "Matruşka: son bebek - boss biter",
                AnimDollLast);
            AddAnim("Matruşka light: mixed 1-2-4-8", "Matruşka ışık: karışık nesiller 1-2-4-8",
                AnimDollLightMixed);
            AddAnim("Matruşka light: eight tiny", "Matruşka ışık: 8 minik", AnimDollLightTiny);
            AddAnim("Matruşka light: body warmth on/off", "Matruşka ışık: gövde sıcaklığı aç/kapa",
                delegate { AnimDollLayer(0); });
            AddAnim("Matruşka light: gold response on/off", "Matruşka ışık: altın yanıtı aç/kapa",
                delegate { AnimDollLayer(1); });
            AddAnim("Matruşka light: lacquer sheen on/off", "Matruşka ışık: cila parlaması aç/kapa",
                delegate { AnimDollLayer(2); });
            AddAnim("Matruşka light: presence light on/off", "Matruşka ışık: zemin ışığı aç/kapa",
                delegate { AnimDollLayer(3); });
            AddAnim("Matruşka light: rim light on/off", "Matruşka ışık: kenar ışığı aç/kapa",
                delegate { AnimDollLayer(4); });
            AddAnim("Matruşka light: motion on/off", "Matruşka ışık: hareket aç/kapa",
                delegate { AnimDollLayer(5); });
            AddAnim("doomed column (İstilacı)", "işaretli sütun (İstilacı)",
                delegate
                {
                    AnimTintMarker(delegate
                    {
                        boardView.ShowDoomedColumn(AnimMiddleColumn(), animInvaderTurns);
                        // Each press steps the countdown 3 -> 2 -> 1 and then fires the
                        // extraction, which is the only way to see all four states.
                        animInvaderTurns--;
                        if (animInvaderTurns < 1)
                        {
                            boardView.PlayColumnExtraction(
                                AnimColumnCubes(AnimMiddleColumn()));
                            animInvaderTurns = 3;
                        }
                    });
                });
            AddAnim("İstilacı COLLECTION (empty/one/full/colours/hard)",
                "İstilacı TAHSİLAT (boş/tek/dolu/renkler/sert)", AnimColumnSweep);
            AddAnim("gravity field (down/right/up/left)",
                "kütleçekim alanı (aşağı/sağ/yukarı/sol)", AnimGravityField);
            AddAnim("clear board markers", "işaretleri temizle", AnimClearMarkers);
            AddAnim("mine shell game (hold open / reveal / one hop / full / re-reveal)",
                "mayın dansı (açık tut / gösterim / tek hamle / tam / yeniden)", AnimMineDance);

            AddAnimHeader("blasts + shake", "patlama + sarsıntı");
            AddAnim("line clear ray: row (combo knob)", "satır ışını (kombo ayarı)",
                delegate { FlashLineAtKnob(AnimBoard(), AnimMiddleRow(), true); });
            AddAnim("line clear ray: column (combo knob)", "sütun ışını (kombo ayarı)",
                delegate { FlashLineAtKnob(AnimBoard(), AnimMiddleColumn(), false); });
            AddAnim("line clear ray: plus (row + column)", "artı ışını (satır + sütun)",
                AnimPlusBlast);
            // With the bang on the first break - which is how a power blast plays it, so the
            // separate "power blast" entry (the same call on the same cells) is gone.
            AddAnim("blast: N cells of plain blocks (cells knob)", "patlama: N hücre (hücre ayarı)",
                delegate
                {
                    FlashCells(AnimCells(), BlastColor, delegate { sfx.Explode(); },
                        AnimCubeFaces(null));
                });
            AddAnim("blast: N cells of the element's blocks (element + cells knobs)",
                "patlama: element blokları (element + hücre ayarı)",
                delegate
                {
                    FlashCells(AnimCells(), ViewUtil.ElementColor(AnimElement()), null,
                        AnimCubeFaces(AnimElement()));
                });
            AddAnim("blast: every element in turn, real blocks (cells knob)",
                "patlama: her element sırayla (gerçek bloklar)", AnimClusterEveryElement);
            AddAnim("blast: N scattered cells (cells knob)",
                "patlama: dağınık N hücre (hücre ayarı)",
                delegate { FlashCells(AnimScatteredCells(), BlastColor, null, AnimCubeFaces(null)); });
            AddAnim("blast: every N in turn, neutral then element",
                "patlama: tüm N'ler sırayla (nötr, sonra element)", AnimClusterEveryCount);
            // A boss REMOVING cubes draws one of the removal variants at random, as the game
            // does; each variant also has an entry of its own. The three bosses that take cubes
            // WITHOUT them vanishing in place keep the old cold mark, and each is played as it
            // happens in its round, on a board of the lab's own (see AnimBossLift).
            AddAnim("removed cells: random variant, as in the game (cells knob)",
                "kaldırılan hücreler: rastgele varyant (oyundaki gibi)",
                delegate { PlayRemoval(AnimCells(), null, AnimCubeFaces(null)); });
            AddAnim("removed cells 1: cold sink (cells knob)",
                "kaldırılan hücreler 1: soğuk kuyu (hücre ayarı)",
                delegate { PlayRemoval(AnimCells(), RemovalVariant.ColdSink, AnimCubeFaces(null)); });
            AddAnim("removed cells 1: cold sink, the element's blocks (element + cells knobs)",
                "kaldırılan hücreler 1: soğuk kuyu (element blokları)",
                delegate
                {
                    PlayRemoval(AnimCells(), RemovalVariant.ColdSink, AnimCubeFaces(AnimElement()));
                });
            AddAnim("removed cells 2: phase fold (cells knob)",
                "kaldırılan hücreler 2: soğuk katlama (hücre ayarı)",
                delegate { PlayRemoval(AnimCells(), RemovalVariant.PhaseFold, AnimCubeFaces(null)); });
            AddAnim("removed cells 2: phase fold, the element's blocks (element + cells knobs)",
                "kaldırılan hücreler 2: soğuk katlama (element blokları)",
                delegate
                {
                    PlayRemoval(AnimCells(), RemovalVariant.PhaseFold, AnimCubeFaces(AnimElement()));
                });
            AddAnim("phase fold debug: compression marks on/off",
                "katlama hata ayıklama: sıkıştırma izleri aç/kapa",
                delegate { AnimFoldToggle(ref PhaseFoldView.Layers.ShowCompressionMarks, "compression marks", "sıkıştırma izleri"); });
            AddAnim("phase fold debug: layer split on/off",
                "katlama hata ayıklama: katman ayrışması aç/kapa",
                delegate { AnimFoldToggle(ref PhaseFoldView.Layers.ShowLayerSplit, "layer split", "katman ayrışması"); });
            AddAnim("phase fold debug: flex on/off",
                "katlama hata ayıklama: bükülme aç/kapa",
                delegate { AnimFoldToggle(ref PhaseFoldView.Layers.ShowFlex, "flex", "bükülme"); });
            AddAnim("phase fold debug: negative ghost on/off",
                "katlama hata ayıklama: negatif iz aç/kapa",
                delegate { AnimFoldToggle(ref PhaseFoldView.Layers.ShowNegativeGhost, "negative ghost", "negatif iz"); });
            AddAnim("phase fold debug: seam mask on/off",
                "katlama hata ayıklama: yarık maskesi aç/kapa",
                delegate { AnimFoldToggle(ref PhaseFoldView.Layers.ShowSeamMask, "seam mask", "yarık maskesi"); });
            AddAnim("removed cells 3: cryo sublimation (cells knob)",
                "kaldırılan hücreler 3: soğuk süblimleşme (hücre ayarı)",
                delegate { PlayRemoval(AnimCells(), RemovalVariant.CryoSublimation, AnimCubeFaces(null)); });
            AddAnim("removed cells 3: cryo sublimation, the element's blocks (element + cells knobs)",
                "kaldırılan hücreler 3: soğuk süblimleşme (element blokları)",
                delegate
                {
                    PlayRemoval(AnimCells(), RemovalVariant.CryoSublimation, AnimCubeFaces(AnimElement()));
                });
            AddAnim("cryo debug: thermal drain on/off",
                "süblimleşme hata ayıklama: ısı çekilmesi aç/kapa",
                delegate { AnimCryoToggle(ref CryoSublimationView.Layers.ShowThermalDrain, "thermal drain", "ısı çekilmesi"); });
            AddAnim("cryo debug: frost mask on/off",
                "süblimleşme hata ayıklama: buz maskesi aç/kapa",
                delegate { AnimCryoToggle(ref CryoSublimationView.Layers.ShowFrostMask, "frost mask", "buz maskesi"); });
            AddAnim("cryo debug: last colour core on/off",
                "süblimleşme hata ayıklama: son renk çekirdeği aç/kapa",
                delegate { AnimCryoToggle(ref CryoSublimationView.Layers.ShowLastColorCore, "last colour core", "son renk çekirdeği"); });
            AddAnim("cryo debug: sublimation erosion on/off",
                "süblimleşme hata ayıklama: kütle aşınması aç/kapa",
                delegate { AnimCryoToggle(ref CryoSublimationView.Layers.ShowSublimationErosion, "sublimation erosion", "kütle aşınması"); });
            AddAnim("cryo debug: vapour ribbons on/off",
                "süblimleşme hata ayıklama: buhar şeritleri aç/kapa",
                delegate { AnimCryoToggle(ref CryoSublimationView.Layers.ShowVaporRibbons, "vapour ribbons", "buhar şeritleri"); });
            AddAnim("cryo debug: frost shell on/off",
                "süblimleşme hata ayıklama: buz kabuğu aç/kapa",
                delegate { AnimCryoToggle(ref CryoSublimationView.Layers.ShowFrostShell, "frost shell", "buz kabuğu"); });
            AddAnim("cryo debug: final dust on/off",
                "süblimleşme hata ayıklama: buz tozu aç/kapa",
                delegate { AnimCryoToggle(ref CryoSublimationView.Layers.ShowFinalDust, "final dust", "buz tozu"); });
            AddAnim("cryo debug: cold residue on/off",
                "süblimleşme hata ayıklama: soğuk iz aç/kapa",
                delegate { AnimCryoToggle(ref CryoSublimationView.Layers.ShowColdResidue, "cold residue", "soğuk iz"); });
            AddAnim("Yürüyen merdiven: the board rides up, the top row is torn off (momentum peel)",
                "yürüyen merdiven: alan yukarı kayar, üst satır sökülür (soğuk sökülme)",
                delegate { AnimBossLift(AnimBossScene.Escalator); });
            AddAnim("Merkezkaç kuvveti: cubes flung outward, the rim is torn off (momentum peel)",
                "merkezkaç kuvveti: küpler dışa itilir, kenardakiler sökülür (soğuk sökülme)",
                delegate { AnimBossLift(AnimBossScene.Centrifuge); });
            // "Kangren" - NECROTIC TAKEOVER. Nine scenes, each about ONE thing the rot does, all on
            // a board of the lab's own and all running the REAL rules on it: the spread picks its
            // own cell (there is only one it can take), and the deaths, the jumps and the cascade
            // are GameBoard.InfectFullLines. The lab only fabricates which cell it finishes a line
            // at. See AnimRot.
            AddAnim("Kangren: the rot takes an EMPTY cell (each press from another side)",
                "kangren: boş hücreye yayılma (her basışta başka yönden)",
                delegate { AnimRot(AnimRotScene.Empty); });
            AddAnim("Kangren: it CONVERTS the cube standing there (each press from another side)",
                "kangren: dolu küpü dönüştürme (her basışta başka yönden)",
                delegate { AnimRot(AnimRotScene.Occupied); });
            AddAnim("Kangren: what it presses against and cannot take (immune cubes)",
                "kangren: bağışık hedef (alamadığı küpler)",
                delegate { AnimRot(AnimRotScene.Immune); });
            AddAnim("Kangren: presence - standing rot and a dead line, nothing happening",
                "kangren: varlık / bekleme (duran kangren ve ölü hat)",
                delegate { AnimRot(AnimRotScene.Presence); });
            AddAnim("Kangren: the turn's bill, felt once through every rotten cube",
                "kangren: tur sonu hasar nabzı",
                delegate { AnimRot(AnimRotScene.Billed); });
            AddAnim("Kangren: a full ROW dies (the bottom row - the jump has nowhere to go)",
                "kangren: tam satır ölümü (en alt satır, atlayacak yer yok)",
                delegate { AnimRot(AnimRotScene.DeadRow); });
            AddAnim("Kangren: a full COLUMN dies (the left column - the jump has nowhere to go)",
                "kangren: tam sütun ölümü (en sol sütun, atlayacak yer yok)",
                delegate { AnimRot(AnimRotScene.DeadColumn); });
            AddAnim("Kangren: a row dies and the rot JUMPS to the edge row (gaps stay empty)",
                "kangren: satır ölür, kangren kenara atlar (boşluklar boş kalır)",
                delegate { AnimRot(AnimRotScene.EdgeJump); });
            AddAnim("Kangren: a CASCADE - row, jump, column, jump, row",
                "kangren: zincirleme atlama (satır → sütun → satır)",
                delegate { AnimRot(AnimRotScene.Chain); });
            AddAnim("rot debug: source direction on/off",
                "kangren hata ayıklama: kaynak yönü aç/kapa",
                delegate { AnimRotToggle(ref GangreneView.Layers.ShowSourceDirection, "source direction", "kaynak yönü"); });
            AddAnim("rot debug: vein layer on/off",
                "kangren hata ayıklama: damar katmanı aç/kapa",
                delegate { AnimRotToggle(ref GangreneView.Layers.ShowVeinLayer, "vein layer", "damar katmanı"); });
            AddAnim("rot debug: crack layer on/off",
                "kangren hata ayıklama: çatlak katmanı aç/kapa",
                delegate { AnimRotToggle(ref GangreneView.Layers.ShowCrackLayer, "crack layer", "çatlak katmanı"); });
            AddAnim("rot debug: cell floor contamination on/off",
                "kangren hata ayıklama: hücre zemini bulaşması aç/kapa",
                delegate { AnimRotToggle(ref GangreneView.Layers.ShowCellFloorContamination, "cell floor contamination", "hücre zemini bulaşması"); });
            AddAnim("rot debug: dead line underlay on/off",
                "kangren hata ayıklama: ölü hat bandı aç/kapa",
                delegate { AnimRotToggle(ref GangreneView.Layers.ShowDeadLineUnderlay, "dead line underlay", "ölü hat bandı"); });
            AddAnim("rot debug: edge transfer path on/off",
                "kangren hata ayıklama: kenara aktarım yolu aç/kapa",
                delegate { AnimRotToggle(ref GangreneView.Layers.ShowEdgeTransferPath, "edge transfer path", "kenara aktarım yolu"); });
            AddAnim("rot debug: target edge conversion on/off",
                "kangren hata ayıklama: kenar küplerinin dönüşümü aç/kapa",
                delegate { AnimRotToggle(ref GangreneView.Layers.ShowTargetEdgeConversion, "target edge conversion", "kenar küplerinin dönüşümü"); });
            AddAnim("rot debug: chain step breaks on/off",
                "kangren hata ayıklama: zincir adım işaretleri aç/kapa",
                delegate { AnimRotToggle(ref GangreneView.Layers.ShowChainStepBreaks, "chain step breaks", "zincir adım işaretleri"); });
            AddAnim("thrown off by a boss's move: the next of 8 directions each press (cells knob)",
                "boss hareketiyle atılma: her basışta sıradaki yön, 8 yön (hücre ayarı)",
                AnimPeelDirection);
            AddAnim("thrown off by a boss's move: riding into holes (escalator, a holed arena)",
                "boss hareketiyle atılma: deliğe çıkış (merdiven, delikli alan)",
                delegate { AnimBossLift(AnimBossScene.Holes); });
            AddAnim("thrown off by a boss's move: blocked target (staged)",
                "boss hareketiyle atılma: engelli hedef (sahnelenmiş)",
                AnimPeelBlocked);
            AddAnim("Yürüyen merdiven — inner movement: each press sparse / dense / nearly full",
                "Yürüyen Merdiven — İç Hareket (her basışta: seyrek / yoğun / neredeyse dolu)",
                delegate { AnimBoardMoveTest(true); });
            AddAnim("Merkezkaç — inner movement: each press round the centre / mixed / dense",
                "Merkezkaç — İç Hareket (her basışta: merkez çevresi / karışık / yoğun)",
                delegate { AnimBoardMoveTest(false); });
            AddAnim("inner movement debug: contact shadow response on/off",
                "iç hareket hata ayıklama: gölge tepkisi aç/kapa",
                delegate { AnimMoveToggle(ref BossMoveView.Layers.ShowContactShadowResponse, "contact shadow response", "gölge tepkisi"); });
            AddAnim("inner movement debug: scale response on/off",
                "iç hareket hata ayıklama: ölçek tepkisi aç/kapa",
                delegate { AnimMoveToggle(ref BossMoveView.Layers.ShowScaleResponse, "scale response", "ölçek tepkisi"); });
            AddAnim("inner movement debug: movement vector on/off",
                "iç hareket hata ayıklama: hareket vektörü aç/kapa",
                delegate { AnimMoveToggle(ref BossMoveView.Layers.ShowMovementVector, "movement vector", "hareket vektörü"); });
            AddAnim("inner movement debug: destination cell on/off",
                "iç hareket hata ayıklama: hedef hücre aç/kapa",
                delegate { AnimMoveToggle(ref BossMoveView.Layers.ShowDestinationCell, "destination cell", "hedef hücre"); });
            AddAnim("inner movement debug: movement proxy on/off",
                "iç hareket hata ayıklama: hareket proxy'si aç/kapa",
                delegate { AnimMoveToggle(ref BossMoveView.Layers.ShowMovementProxy, "movement proxy", "hareket proxy'si"); });
            AddAnim("momentum peel debug: tension on/off",
                "sökülme hata ayıklama: gerilme aç/kapa",
                delegate { AnimPeelToggle(ref MomentumPeelView.Layers.ShowTension, "tension", "gerilme"); });
            AddAnim("momentum peel debug: lamination on/off",
                "sökülme hata ayıklama: katmanlar aç/kapa",
                delegate { AnimPeelToggle(ref MomentumPeelView.Layers.ShowLamination, "lamination", "katmanlar"); });
            AddAnim("momentum peel debug: flecks on/off",
                "sökülme hata ayıklama: kıymıklar aç/kapa",
                delegate { AnimPeelToggle(ref MomentumPeelView.Layers.ShowFlecks, "flecks", "kıymıklar"); });
            AddAnim("momentum peel debug: residue on/off",
                "sökülme hata ayıklama: hareket izi aç/kapa",
                delegate { AnimPeelToggle(ref MomentumPeelView.Layers.ShowResidue, "residue", "hareket izi"); });
            AddAnim("momentum peel debug: board clip on/off",
                "sökülme hata ayıklama: tahta maskesi aç/kapa",
                delegate { AnimPeelToggle(ref MomentumPeelView.Layers.ShowBoardClip, "board clip", "tahta maskesi"); });
            AddAnim("clean sweep: board flash + confetti", "temizlik: alan ışığı + yağmur",
                delegate { EmitSweepConfetti(); });
            AddAnim("dynamite: blast + smoke", "dinamit: patlama + duman",
                delegate { FlashDynamite(DynamiteCenter(null)); });
            AddAnim("dynamite smoke alone", "dinamit dumanı (tek başına)", PlayDynamiteSmoke);
            AddAnim("süpürge delayed blast", "süpürge gecikmeli patlama",
                delegate { StartCoroutine(SupurgeBlastRoutine(AnimCells())); });
            AddAnim("infection: 1-cell block ruptures", "enfeksiyon: tek hücreli blok patlar",
                delegate { AnimInfectionBurst(1, false); });
            // Only three, not the four you might expect: a LATER detonation is a block rupturing
            // with no arms, which is exactly what the middle entry already is. A fourth entry
            // running the same call would say the two differ when they do not.
            AddAnim("infection: block ruptures, no spread",
                "enfeksiyon: blok patlar, bulaşma yok",
                delegate { AnimInfectionBurst(0, false); });
            AddAnim("infection: FIRST burst + spread", "enfeksiyon: İLK patlama + bulaşma",
                delegate { AnimInfectionBurst(0, true); });
            // A defective smuggled block falls through the SAME way whatever it is - the rules
            // check FallsThrough before anything else touches the board - so these are one
            // animation on different blocks: the element knob's, and every type in turn.
            AddAnim("falling cubes: defective block (element knob)",
                "düşen küpler: defolu blok (element ayarı)", AnimFallingCubes);
            AddAnim("falling cubes: every block type in turn",
                "düşen küpler: tüm blok türleri sırayla", AnimFallingCubesEveryType);
            AddAnim("camera shake (combo knob)", "kamera sarsıntısı (kombo ayarı)",
                delegate { ShakeForBlast(false, false, animCombo); });
            AddAnim("camera shake: sweep", "kamera sarsıntısı: temizlik",
                delegate { ShakeForBlast(false, true, animCombo); });
            AddAnim("camera shake: dynamite", "kamera sarsıntısı: dinamit",
                delegate { ShakeForBlast(true, false, animCombo); });

            AddAnimHeader("popups", "yazılar");
            AddAnim("COMBO xN (combo knob)", "KOMBO xN (kombo ayarı)",
                delegate { SpawnComboPopup(Mathf.Max(2, animCombo)); });
            AddAnim("CLEAN SWEEP!", "TEMİZLİK!", delegate { SpawnSweepPopup(); });
            AddAnim("DYNAMITE!", "DİNAMİT!", delegate { SpawnDynamitePopup(); });
            AddAnim("TARGET HIT!", "HEDEF VURULDU!", delegate { SpawnTargetPopup(); });
            AddAnim("FED (Tamagotchi)", "YEDİ (Tamagotchi)",
                delegate
                {
                    FloatingTextFx.Spawn(transform, new Vector2(0f, -2.2f),
                        Loc.Pick("FED", "YEDİ"), new Color(0.6f, 0.9f, 0.5f), 54, 0.05f);
                });
            AddAnim("+score (a sale)", "+puan (satış)",
                delegate
                {
                    FloatingTextFx.Spawn(transform, new Vector2(0f, -1.4f), "+120",
                        new Color(1f, 0.92f, 0.45f), 60, 0.05f);
                });
            AddAnim("worthless (a sale)", "değersiz (satış)",
                delegate
                {
                    FloatingTextFx.Spawn(transform, new Vector2(0f, -1.4f),
                        Loc.Pick("worthless", "değersiz"), new Color(0.6f, 0.6f, 0.6f), 50, 0.045f);
                });

            AddAnimHeader("bars + market", "barlar + market");
            AddAnim("joker panel pulse", "joker paneli nabzı",
                delegate { AnimPulseJoker(); });
            AddAnim("joker sold shrink", "joker satıldı", AnimSellJoker);
            AddAnim("power panel pulse", "güç paneli nabzı",
                delegate { AnimPulsePower(); });
            AddAnim("power sold shrink", "güç satıldı", AnimSellPower);
            AddAnim("market: block buy flight", "market: blok alma uçuşu",
                delegate { AnimMarketBuy(MarketOfferKind.Block); });
            AddAnim("market: joker buy flight", "market: joker alma uçuşu",
                delegate { AnimMarketBuy(MarketOfferKind.Joker); });
            AddAnim("market: power buy flight", "market: güç alma uçuşu",
                delegate { AnimMarketBuy(MarketOfferKind.Power); });
            AddAnim("deck overlay: sell flight", "deste ekranı: satış uçuşu", AnimDeckSell);

            AddAnimHeader("ambience", "atmosfer");
            AddAnim("overtime flame (overtime knob)", "uzatma alevi (uzatma ayarı)",
                delegate { flameStreak.SetState(animOvertime, boardView.WorldRect); });
            AddAnim("overtime flame off", "uzatma alevi kapalı",
                delegate { flameStreak.SetState(0, boardView.WorldRect); });
            AddAnim("sweep bling (sweep-count pitch)", "temizlik sesi (sayıya göre tiz)",
                delegate { sfx.CleanSweep(1f + 0.12f * Mathf.Min(animSweeps - 1, 8)); });
            AddAnim("explosion sound", "patlama sesi", delegate { sfx.Explode(); });
            AddAnim("flame sound", "alev sesi", delegate { sfx.Flame(); });

            AddAnimHeader("yılan", "yılan");
            // "YILAN" - every scenario on a board of the lab's own, played through the same seams
            // the game plays it through (PlaySnakeBody / PlaySnakeScene). The lab fabricates the
            // ARGUMENTS - the shape of the snake, what is standing in front of it, how many lines
            // crossed it - and the animation is then exactly the one a real turn would get.
            AddAnim("Yılan: wakes up, 8 segments", "yılan: uyanıyor, 8 segment",
                delegate { AnimSnake(AnimSnakeScene.Spawn8); });
            AddAnim("Yılan: wakes up, 12 segments", "yılan: uyanıyor, 12 segment",
                delegate { AnimSnake(AnimSnakeScene.Spawn12); });
            AddAnim("Yılan: wakes up, 20 segments", "yılan: uyanıyor, 20 segment",
                delegate { AnimSnake(AnimSnakeScene.Spawn20); });
            AddAnim("Yılan: idle - the body's own slow wave", "yılan: bekleme (gövdenin kas dalgası)",
                delegate { AnimSnake(AnimSnakeScene.Idle); });
            AddAnim("Yılan: slides ONE cell", "yılan: bir hücre kayar",
                delegate { AnimSnake(AnimSnakeScene.Slide1); });
            AddAnim("Yılan: slides THREE cells", "yılan: üç hücre kayar",
                delegate { AnimSnake(AnimSnakeScene.Slide3); });
            AddAnim("Yılan: a long slide, right across the arena", "yılan: uzun kayma (alanın boyu)",
                delegate { AnimSnake(AnimSnakeScene.SlideLong); });
            AddAnim("Yılan: slides off in a NEW direction (the head comes round)",
                "yılan: yön değiştirerek kayar (baş dönüyor)",
                delegate { AnimSnake(AnimSnakeScene.SlideTurn); });
            AddAnim("Yılan: boxed in - it tries and cannot move", "yılan: sıkıştı, gidecek yer yok",
                delegate { AnimSnake(AnimSnakeScene.Stuck); });
            AddAnim("Yılan: eats a plain block", "yılan: sade blok yer",
                delegate { AnimSnake(AnimSnakeScene.EatNormal); });
            AddAnim("Yılan: eats OBSIDIAN (it does not care)", "yılan: obsidyen yer (umurunda değil)",
                delegate { AnimSnake(AnimSnakeScene.EatObsidian); });
            AddAnim("Yılan: eats GOLD (no coins, no melt - swallowed)",
                "yılan: altın yer (erime yok, yutuyor)",
                delegate { AnimSnake(AnimSnakeScene.EatGold); });
            AddAnim("Yılan: eats and GROWS (the swelling behind the head)",
                "yılan: yer ve uzar (başın arkasındaki şişkinlik)",
                delegate { AnimSnake(AnimSnakeScene.EatAndGrow); });
            AddAnim("Yılan: ONE tail segment cut", "yılan: bir kuyruk kesilir",
                delegate { AnimSnake(AnimSnakeScene.Cut1); });
            AddAnim("Yılan: TWO cut in one turn", "yılan: aynı turda iki kesik",
                delegate { AnimSnake(AnimSnakeScene.Cut2); });
            AddAnim("Yılan: THREE cut in one turn", "yılan: aynı turda üç kesik",
                delegate { AnimSnake(AnimSnakeScene.Cut3); });
            AddAnim("Yılan: the last segment - the boss is beaten",
                "yılan: son segment (boss yenildi)",
                delegate { AnimSnake(AnimSnakeScene.Defeat); });
            AddAnim("Yılan: a whole turn - slide, bite, growth",
                "yılan: tam tur dizisi (kayma → yeme → uzama)",
                delegate { AnimSnake(AnimSnakeScene.FullTurn); });
            AddAnim("Yılan: a whole line-clear - the line, the signal, the tail",
                "yılan: hat dizisi (patlama → sinyal → kuyruk)",
                delegate { AnimSnake(AnimSnakeScene.LineClear); });
            AddAnim("Yılan test: the four directions, one per press",
                "yılan testi: dört yön (her basışta biri)",
                delegate { AnimSnake(AnimSnakeScene.Directions); });
            AddAnim("Yılan test: body shapes - straight, one corner, several, S, serpentine",
                "yılan testi: gövde şekilleri (düz, tek köşe, çoklu, S, yılanvari)",
                delegate { AnimSnake(AnimSnakeScene.Shapes); });
            AddAnim("Yılan test: the bite in all four directions",
                "yılan testi: dört yönde ısırma",
                delegate { AnimSnake(AnimSnakeScene.BiteDirections); });
            AddAnimHeader("snake: beaten - the stolen colours let go",
                "yılan: yenilgi - çalınan renklerin serbest kalması");
            // THE BOSS'S END, and the one animation whose subject is what it TOOK. Each entry
            // hands in a different round's worth of swallowed colour, because an animation that
            // looked the same every time would be saying nothing about the round it ended.
            AddAnim("Yılan beaten: whatever THIS round actually ate",
                "yılan yenilgi: bu raundun gerçekten yediği",
                delegate { AnimSnakeDefeat(null); });
            AddAnim("Yılan beaten: the snake's own colours (it ate nothing)",
                "yılan yenilgi: yılanın kendi renkleri (hiç yememiş)",
                delegate { AnimSnakeDefeat(new Color[0]); });
            AddAnim("Yılan beaten: red + blue + gold",
                "yılan yenilgi: kırmızı + mavi + altın",
                delegate
                {
                    AnimSnakeDefeat(new[] { new Color(0.93f, 0.36f, 0.42f),
                        new Color(0.35f, 0.6f, 1f), new Color(1f, 0.8f, 0.25f) });
                });
            AddAnim("Yılan beaten: purple + cyan + green",
                "yılan yenilgi: mor + camgöbeği + yeşil",
                delegate
                {
                    AnimSnakeDefeat(new[] { new Color(0.68f, 0.42f, 0.9f),
                        new Color(0.35f, 0.85f, 0.88f), new Color(0.38f, 0.82f, 0.45f) });
                });
            AddAnim("Yılan beaten: gold + obsidian + red",
                "yılan yenilgi: altın + obsidyen + kırmızı",
                delegate
                {
                    AnimSnakeDefeat(new[] { new Color(1f, 0.8f, 0.25f),
                        new Color(0.25f, 0.22f, 0.3f), new Color(0.88f, 0.2f, 0.15f) });
                });
            AddAnim("Yılan beaten: six colours, as a stress test",
                "yılan yenilgi: altı renk, zorlama testi",
                delegate
                {
                    AnimSnakeDefeat(new[] { new Color(0.93f, 0.36f, 0.42f),
                        new Color(1f, 0.62f, 0.2f), new Color(1f, 0.8f, 0.25f),
                        new Color(0.38f, 0.82f, 0.45f), new Color(0.35f, 0.6f, 1f),
                        new Color(0.68f, 0.42f, 0.9f) });
                });
            AddAnim("Yılan beaten: QUARTER speed (RESET puts it back)",
                "yılan yenilgi: ÇEYREK hız (RESET geri alır)",
                delegate
                {
                    Time.timeScale = 0.25f;
                    animSpeedIndex = 1;
                    AnimSnakeDefeat(new[] { new Color(1f, 0.8f, 0.25f),
                        new Color(0.35f, 0.6f, 1f), new Color(0.68f, 0.42f, 0.9f) });
                });
            AddAnim("snake defeat debug: colour pockets on/off",
                "yılan yenilgi hata ayıklama: renk cepleri aç/kapa",
                delegate { AnimSnakeToggle(ref SnakeDefeatView.Layers.ShowColorPockets,
                    "colour pockets", "renk cepleri"); });
            AddAnim("snake defeat debug: the flow to the middle on/off",
                "yılan yenilgi hata ayıklama: merkeze akış aç/kapa",
                delegate { AnimSnakeToggle(ref SnakeDefeatView.Layers.ShowColorFlow,
                    "colour flow", "renk akışı"); });
            AddAnim("snake defeat debug: the collapse on/off",
                "yılan yenilgi hata ayıklama: çöküş aç/kapa",
                delegate { AnimSnakeToggle(ref SnakeDefeatView.Layers.ShowCollapse,
                    "collapse", "çöküş"); });
            AddAnim("snake defeat debug: the colour knot on/off",
                "yılan yenilgi hata ayıklama: renk yumağı aç/kapa",
                delegate { AnimSnakeToggle(ref SnakeDefeatView.Layers.ShowKnot,
                    "knot", "yumak"); });
            AddAnim("snake defeat debug: the ribbons on/off",
                "yılan yenilgi hata ayıklama: hüzmeler aç/kapa",
                delegate { AnimSnakeToggle(ref SnakeDefeatView.Layers.ShowRibbons,
                    "ribbons", "hüzmeler"); });
            AddAnim("snake defeat debug: the secondary threads on/off",
                "yılan yenilgi hata ayıklama: ince iplikler aç/kapa",
                delegate { AnimSnakeToggle(ref SnakeDefeatView.Layers.ShowThreads,
                    "threads", "iplikler"); });
            AddAnim("snake defeat debug: the motes on/off",
                "yılan yenilgi hata ayıklama: moteler aç/kapa",
                delegate { AnimSnakeToggle(ref SnakeDefeatView.Layers.ShowMotes,
                    "motes", "moteler"); });
            AddAnim("snake defeat debug: the board's wave on/off",
                "yılan yenilgi hata ayıklama: tahtanın dalgası aç/kapa",
                delegate { AnimSnakeToggle(ref SnakeDefeatView.Layers.ShowBoardWave,
                    "board wave", "tahta dalgası"); });
            AddAnim("snake defeat debug: the cell reflections on/off",
                "yılan yenilgi hata ayıklama: hücre yansımaları aç/kapa",
                delegate { AnimSnakeToggle(ref SnakeDefeatView.Layers.ShowCellReflections,
                    "cell reflections", "hücre yansımaları"); });
            AddAnim("snake defeat debug: the final glint on/off",
                "yılan yenilgi hata ayıklama: son parıltı aç/kapa",
                delegate { AnimSnakeToggle(ref SnakeDefeatView.Layers.ShowFinalGlint,
                    "final glint", "son parıltı"); });
            AddAnimHeader("snake: the bite, one block at a time",
                "yılan: ısırma (hero) - blok blok");
            // THE BLOCK'S MATTER, not the block. Everything under this heading is one hero event
            // seen from a different side: a different block type, a different direction, or slowed
            // down far enough to see whether the block is really coming apart or just shrinking.
            AddAnim("Yılan bite: a plain block, a new colour each press",
                "yılan ısırma: sıradan blok, her basışta başka renk",
                delegate
                {
                    animEatColour++;
                    AnimSnake(AnimSnakeScene.EatColour);
                });
            AddAnim("Yılan bite: water", "yılan ısırma: su",
                delegate { AnimSnake(AnimSnakeScene.EatWater); });
            AddAnim("Yılan bite: fire", "yılan ısırma: ateş",
                delegate { AnimSnake(AnimSnakeScene.EatFire); });
            AddAnim("Yılan bite: gold", "yılan ısırma: altın",
                delegate { AnimSnake(AnimSnakeScene.EatGold); });
            AddAnim("Yılan bite: obsidian", "yılan ısırma: obsidyen",
                delegate { AnimSnake(AnimSnakeScene.EatObsidian); });
            AddAnim("Yılan bite: every direction in turn",
                "yılan ısırma: sırayla her yön",
                delegate { AnimSnake(AnimSnakeScene.BiteDirections); });
            AddAnim("Yılan bite: HALF speed (RESET puts it back)",
                "yılan ısırma: YARIM hız (RESET geri alır)",
                delegate
                {
                    Time.timeScale = 0.5f;
                    animSpeedIndex = 1;
                    AnimSnake(AnimSnakeScene.EatColour);
                });
            AddAnim("Yılan bite: QUARTER speed - is the block really coming apart?",
                "yılan ısırma: ÇEYREK hız - blok gerçekten sökülüyor mu?",
                delegate
                {
                    Time.timeScale = 0.25f;
                    animSpeedIndex = 1;
                    AnimSnake(AnimSnakeScene.EatColour);
                });
            AddAnim("snake bite debug: the extraction front",
                "yılan ısırma hata ayıklama: çıkarma cephesi",
                delegate { AnimSnakeToggle(ref SnakeEatView.Layers.ShowExtractionMask,
                    "extraction front", "çıkarma cephesi"); });
            AddAnim("snake bite debug: filament paths",
                "yılan ısırma hata ayıklama: filament yolları",
                delegate { AnimSnakeToggle(ref SnakeEatView.Layers.ShowFilamentPaths,
                    "filament paths", "filament yolları"); });
            AddAnim("snake bite debug: where the light is on each filament",
                "yılan ısırma hata ayıklama: filamentteki ışığın yeri",
                delegate { AnimSnakeToggle(ref SnakeEatView.Layers.ShowFilamentFlow,
                    "filament flow", "filament akışı"); });
            AddAnim("snake bite debug: motes on/off",
                "yılan ısırma hata ayıklama: moteler aç/kapa",
                delegate { AnimSnakeToggle(ref SnakeEatView.Layers.ShowMotes,
                    "motes", "moteler"); });
            AddAnim("snake bite debug: the mouth cavity",
                "yılan ısırma hata ayıklama: ağız boşluğu",
                delegate { AnimSnakeToggle(ref SnakeEatView.Layers.ShowMouthMask,
                    "mouth cavity", "ağız boşluğu"); });
            AddAnim("snake bite debug: the gulp package",
                "yılan ısırma hata ayıklama: yutma paketi",
                delegate { AnimSnakeToggle(ref SnakeEatView.Layers.ShowGulpPackage,
                    "gulp package", "yutma paketi"); });
            AddAnim("snake bite debug: the three colours taken off the block",
                "yılan ısırma hata ayıklama: bloktan alınan üç renk",
                delegate { AnimSnakeToggle(ref SnakeEatView.Layers.ShowBlockColorSampling,
                    "block colours", "blok renkleri"); });
            AddAnim("snake bite debug: the final core on/off",
                "yılan ısırma hata ayıklama: son çekirdek aç/kapa",
                delegate { AnimSnakeToggle(ref SnakeEatView.Layers.ShowFinalCore,
                    "final core", "son çekirdek"); });
            AddAnim("snake bite debug: the erosion edge light on/off",
                "yılan ısırma hata ayıklama: erozyon kenar ışığı aç/kapa",
                delegate { AnimSnakeToggle(ref SnakeEatView.Layers.ShowErosionEdge,
                    "erosion edge", "erozyon kenarı"); });
            AddAnim("snake bite debug: the main ribbon on/off",
                "yılan ısırma hata ayıklama: ana hüzme aç/kapa",
                delegate { AnimSnakeToggle(ref SnakeEatView.Layers.ShowMainRibbon,
                    "main ribbon", "ana hüzme"); });
            AddAnim("snake bite debug: the secondary ribbons on/off",
                "yılan ısırma hata ayıklama: ikincil hüzmeler aç/kapa",
                delegate { AnimSnakeToggle(ref SnakeEatView.Layers.ShowSecondaryRibbons,
                    "secondary ribbons", "ikincil hüzmeler"); });
            AddAnim("snake bite debug: the mouth collector on/off",
                "yılan ısırma hata ayıklama: ağız toplayıcısı aç/kapa",
                delegate { AnimSnakeToggle(ref SnakeEatView.Layers.ShowMouthCollector,
                    "mouth collector", "ağız toplayıcısı"); });
            AddAnim("snake bite debug: the head's reflected light on/off",
                "yılan ısırma hata ayıklama: kafadaki yansıyan ışık aç/kapa",
                delegate { AnimSnakeToggle(ref SnakeEatView.Layers.ShowHeadReflection,
                    "head reflection", "kafa yansıması"); });
            AddAnim("Yılan: the bite, in slow motion (RESET puts the speed back)",
                "yılan: ısırma hero, yavaş çekim (hız RESET ile geri döner)",
                delegate
                {
                    Time.timeScale = 0.3f;
                    animSpeedIndex = 1;
                    AnimSnake(AnimSnakeScene.EatNormal);
                });
            // THE VFX LAYERS, one at a time. The point of these is to be able to SEE what each
            // layer is worth: turn the motion off and the surface, shadow and contact work is all
            // that is left; turn the material off and the motion has to carry it alone.
            AddAnim("snake vfx: motion on/off", "yılan vfx: hareket aç/kapa",
                delegate { AnimSnakeToggle(ref SnakeView.Layers.ShowSnakeMotion, "motion", "hareket"); });
            AddAnim("snake vfx: contact shadows on/off", "yılan vfx: temas gölgeleri aç/kapa",
                delegate { AnimSnakeToggle(ref SnakeVfxController.Layers.ShowSnakeShadows, "shadows", "gölgeler"); });
            AddAnim("snake vfx: material response on/off", "yılan vfx: materyal tepkisi aç/kapa",
                delegate { AnimSnakeToggle(ref SnakeVfxController.Layers.ShowSnakeMaterialFx, "material response", "materyal tepkisi"); });
            AddAnim("snake vfx: particles on/off", "yılan vfx: parçacıklar aç/kapa",
                delegate { AnimSnakeToggle(ref SnakeVfxController.Layers.ShowSnakeParticles, "particles", "parçacıklar"); });
            AddAnim("snake vfx: board contact on/off", "yılan vfx: tahta teması aç/kapa",
                delegate { AnimSnakeToggle(ref SnakeVfxController.Layers.ShowSnakeBoardContact, "board contact", "tahta teması"); });
            AddAnim("snake vfx: every layer back on", "yılan vfx: bütün katmanları geri aç",
                delegate
                {
                    SnakeVfxController.Layers.AllOn();
                    SnakeView.Layers.ShowSnakeMotion = true;
                    animLastLabel = Loc.Pick("snake vfx: all layers on",
                        "yılan vfx: bütün katmanlar açık");
                    if (AnimLabOpen)
                    {
                        RedrawAnimationLab();
                    }
                });
            AddAnim("snake debug: body indices on/off",
                "yılan hata ayıklama: segment numaraları aç/kapa",
                delegate { AnimSnakeToggle(ref SnakeView.Layers.ShowSnakeBodyIndices, "body indices", "segment numaraları"); });
            AddAnim("snake debug: head path on/off",
                "yılan hata ayıklama: başın yolu aç/kapa",
                delegate { AnimSnakeToggle(ref SnakeView.Layers.ShowSnakeHeadPath, "head path", "başın yolu"); });
            AddAnim("snake debug: movement direction on/off",
                "yılan hata ayıklama: hareket yönü aç/kapa",
                delegate { AnimSnakeToggle(ref SnakeView.Layers.ShowSnakeMovementDirection, "movement direction", "hareket yönü"); });
            AddAnim("snake debug: topology on/off",
                "yılan hata ayıklama: topoloji aç/kapa",
                delegate { AnimSnakeToggle(ref SnakeView.Layers.ShowSnakeTopology, "topology", "topoloji"); });
            AddAnim("snake debug: eaten cell on/off",
                "yılan hata ayıklama: yenen hücre aç/kapa",
                delegate { AnimSnakeToggle(ref SnakeView.Layers.ShowSnakeEatenCell, "eaten cell", "yenen hücre"); });
            AddAnim("snake debug: cut trigger on/off",
                "yılan hata ayıklama: kesik tetikleyicisi aç/kapa",
                delegate { AnimSnakeToggle(ref SnakeView.Layers.ShowSnakeCutTrigger, "cut trigger", "kesik tetikleyicisi"); });
            AddAnim("snake debug: proxy positions on/off",
                "yılan hata ayıklama: vekil konumlar aç/kapa",
                delegate { AnimSnakeToggle(ref SnakeView.Layers.ShowSnakeVisualProxyPositions, "proxy positions", "vekil konumlar"); });
            AddAnimHeader("hidrolik pres", "hidrolik pres");
            // "HIDROLIK PRES" - every scenario, on a board of the lab's own, with the REAL rules
            // run on it (GameBoard.Compress / GameBoard.Expand through their reporting overloads).
            // The lab fabricates the ARGUMENTS - what is in the four cells, what is standing in the
            // way when it opens - and the animation is then exactly the one a real press would get.
            // Each entry HOLDS what it ends on, like every other entry, until RESET or closing the
            // lab puts the round back.
            AddAnim("Pres: four cubes -> one (the squeeze, end to end)",
                "pres: dört küp → bir (sıkıştırma, baştan sona)",
                delegate { AnimPress(AnimPressScene.Full4); });
            AddAnim("Pres: three cubes and ONE HOLE (the hole is stored too)",
                "pres: üç küp ve BİR BOŞLUK (boşluk da saklanıyor)",
                delegate { AnimPress(AnimPressScene.Three); });
            AddAnim("Pres: two cubes, two holes", "pres: iki küp, iki boşluk",
                delegate { AnimPress(AnimPressScene.Two); });
            AddAnim("Pres: one cube, three holes", "pres: bir küp, üç boşluk",
                delegate { AnimPress(AnimPressScene.One); });
            AddAnim("Pres: FOUR HOLES - it presses nothing and still shuts",
                "pres: DÖRT BOŞLUK - hiçbir şeyi preslemeden kapanıyor",
                delegate { AnimPress(AnimPressScene.Empty); });
            AddAnim("Pres: mixed materials (each lamina keeps its own)",
                "pres: karışık materyal (her katman kendi materyalini koruyor)",
                delegate { AnimPress(AnimPressScene.Mixed); });
            AddAnim("Pres: GOLD and OBSIDIAN stored inside it",
                "pres: içinde ALTIN ve OBSİDYEN saklanıyor",
                delegate { AnimPress(AnimPressScene.Stone); });
            AddAnim("Pres wait 1: just locked - shallow dimple, calm seams, one crease",
                "pres bekleme 1: yeni kilitlendi - sığ çukur, sakin dikişler, tek çizik",
                delegate { AnimPress(AnimPressScene.Idle1); });
            AddAnim("Pres wait 2: pressure settling into the shell",
                "pres bekleme 2: basınç kabuğa yerleşiyor",
                delegate { AnimPress(AnimPressScene.Idle2); });
            AddAnim("Pres wait 3: the shell is carrying real load",
                "pres bekleme 3: kabuk gerçekten yük taşıyor",
                delegate { AnimPress(AnimPressScene.Idle3); });
            AddAnim("Pres wait FINAL: at the limit, close to release",
                "pres bekleme SON: sınırda, açılmaya yakın",
                delegate { AnimPress(AnimPressScene.Idle4); });
            AddAnim("Pres: ADVANCE A TURN (the pressure tick)",
                "pres: BİR TUR İLERLET (basınç tıkı)", AnimPressAdvanceTurn);
            AddAnim("Pres: CLEAN release - nothing in the way",
                "pres: TEMİZ açılma - önünde hiçbir şey yok",
                delegate { AnimPress(AnimPressScene.CleanRelease); });
            AddAnim("Pres: release shoving ONE cube", "pres: açılırken BİR küpü itiyor",
                delegate { AnimPress(AnimPressScene.PushOne); });
            AddAnim("Pres: release shoving a LONG CHAIN", "pres: açılırken UZUN ZİNCİR itiyor",
                delegate { AnimPress(AnimPressScene.PushChain); });
            AddAnim("Pres: a cube shoved OFF THE BOARD (one movement, no stop at the rim)",
                "pres: bir küp ALANDAN TAŞIYOR (tek hareket, kenarda durmuyor)",
                delegate { AnimPress(AnimPressScene.PushOffBoard); });
            AddAnim("Pres: a straight side shut by GOLD (no reroute exists there - it fails)",
                "pres: düz yönü ALTIN kapatıyor (orada yön değiştirme yok - patlıyor)",
                delegate { AnimPress(AnimPressScene.BlockedGold); });
            AddAnim("Pres: a straight side shut by OBSIDIAN",
                "pres: düz yönü OBSİDYEN kapatıyor",
                delegate { AnimPress(AnimPressScene.BlockedObsidian); });
            AddAnim("Pres: the CORNER shut one way - the pressure reroutes and it opens the other",
                "pres: KÖŞE bir yönden kapalı - basınç yön değiştirip öbür yöne açılıyor",
                delegate { AnimPress(AnimPressScene.Rerouted); });
            AddAnim("Pres: BOTH of the corner's ways shut - PRESSURE VESSEL FAILURE",
                "pres: köşenin İKİ yönü de kapalı - BASINÇ KABI ÇÖKÜYOR",
                delegate { AnimPress(AnimPressScene.Failure); });
            AddAnim("Pres: the failure SHEARS the gold and obsidian around it",
                "pres: çöküş etrafındaki altın ve obsidyeni EZİYOR",
                delegate { AnimPress(AnimPressScene.FailureStone); });
            AddAnim("Pres: BROKEN while shut - no release, the stored picture is gone",
                "pres: sıkışıkken KIRILDI - açılma yok, saklanan resim gitti",
                delegate { AnimPress(AnimPressScene.DestroyedShut); });
            AddAnim("Pres: the WHOLE life - squeeze, four turns, release",
                "pres: TÜM yaşam - sıkıştırma, dört tur, açılma",
                delegate { AnimPress(AnimPressScene.Lifecycle); });
            AddAnim("Pres: the whole life, ending in FAILURE",
                "pres: tüm yaşam, sonu ÇÖKÜŞ",
                delegate { AnimPress(AnimPressScene.FailureLifecycle); });
            AddAnim("Pres switch: the four pressure jaws", "pres anahtarı: dört basınç çenesi",
                delegate { AnimPressToggle(ref HydraulicPressView.Layers.ShowJaws,
                    "jaws", "çeneler"); });
            AddAnim("Pres switch: the laminae themselves", "pres anahtarı: katmanların kendisi",
                delegate { AnimPressToggle(ref HydraulicPressView.Layers.ShowLaminae,
                    "laminae", "katmanlar"); });
            AddAnim("Pres switch: the empty quadrants' imprints",
                "pres anahtarı: boş bölmelerin izleri",
                delegate { AnimPressToggle(ref HydraulicPressView.Layers.ShowNullImprints,
                    "null imprints", "boşluk izleri"); });
            AddAnim("Pres switch: the slate shell", "pres anahtarı: slate kabuk",
                delegate { AnimPressToggle(ref HydraulicPressView.Layers.ShowShell,
                    "shell", "kabuk"); });
            AddAnim("Pres switch: the directional pressure front",
                "pres anahtarı: yönlü basınç cephesi",
                delegate { AnimPressToggle(ref HydraulicPressView.Layers.ShowPressureFront,
                    "pressure front", "basınç cephesi"); });
            AddAnim("Pres switch: the pushed cubes' own response",
                "pres anahtarı: itilen küplerin kendi tepkisi",
                delegate { AnimPressToggle(ref HydraulicPressView.Layers.ShowPushResponse,
                    "push response", "itme tepkisi"); });
            AddAnim("Pres switch: the quadrant seams + stored-colour memory",
                "pres anahtarı: bölme dikişleri + saklanan renk hafızası",
                delegate { AnimPressToggle(ref CompressedCubeView.Layers.ShowSeams,
                    "seams", "dikişler"); });
            AddAnim("Pres switch: the central pressure dimple",
                "pres anahtarı: merkezdeki basınç çukuru",
                delegate { AnimPressToggle(ref CompressedCubeView.Layers.ShowDimple,
                    "dimple", "çukur"); });
            AddAnim("Pres switch: the quadrant pressure scars (the countdown)",
                "pres anahtarı: bölme basınç çizikleri (geri sayım)",
                delegate { AnimPressToggle(ref CompressedCubeView.Layers.ShowScars,
                    "pressure scars", "basınç çizikleri"); });
            AddAnim("Pres switch: the compressed cube's contact shadow",
                "pres anahtarı: preslenmiş küpün temas gölgesi",
                delegate { AnimPressToggle(ref CompressedCubeView.Layers.ShowShadow,
                    "contact shadow", "temas gölgesi"); });
            AddAnim("Pres switch: the failure's inward collapse",
                "pres anahtarı: çöküşün içe göçmesi",
                delegate { AnimPressToggle(ref PressureVesselView.Layers.ShowCollapse,
                    "inward collapse", "içe göçme"); });
            AddAnim("Pres switch: the failure's overpressure burst",
                "pres anahtarı: çöküşün aşırı basınç dalgası",
                delegate { AnimPressToggle(ref PressureVesselView.Layers.ShowBurst,
                    "burst", "dalga"); });
            AddAnim("Pres switch: the indestructible shear",
                "pres anahtarı: kırılmaz küplerin ezilmesi",
                delegate { AnimPressToggle(ref PressureVesselView.Layers.ShowShear,
                    "shear", "ezilme"); });
            AddAnim("Pres switch: the failure's residue", "pres anahtarı: çöküş kalıntısı",
                delegate { AnimPressToggle(ref PressureVesselView.Layers.ShowResidue,
                    "residue", "kalıntı"); });
            AddAnim("Pres switch: ALL press layers back on",
                "pres anahtarı: TÜM pres katmanları geri açık",
                delegate
                {
                    HydraulicPressView.Layers.AllOn();
                    CompressedCubeView.Layers.AllOn();
                    PressureVesselView.Layers.AllOn();
                    animLastLabel = Loc.Pick("every press layer on", "tüm pres katmanları açık");
                    if (AnimLabOpen)
                    {
                        RedrawAnimationLab();
                    }
                });
            AddAnimHeader("parazit / konak küp", "parazit / konak küp");
            // "PARAZIT" - the clasp on a host cube, on a board of the lab's own, with the REAL
            // rules run on it: the refusals come from GameBoard.DestroyCube / DestroyCubeForced
            // writing them down, exactly as they do in a turn.
            AddAnim("Konak: blue cube under the clasp", "konak: mavi küp kenedin altında",
                delegate { AnimHost(AnimHostScene.Blue); });
            AddAnim("Konak: red cube (its own colour survives)",
                "konak: kırmızı küp (kendi rengi duruyor)",
                delegate { AnimHost(AnimHostScene.Red); });
            AddAnim("Konak HERO: OBSIDIAN - dark on dark, the parasite must still read",
                "konak HERO: OBSİDYEN - koyu üstüne koyu, parazit yine de okunmalı",
                delegate { AnimHost(AnimHostScene.Purple); });
            AddAnim("Konak HERO: GOLD - still gold, but drained and dusty",
                "konak HERO: ALTIN - hâlâ altın, ama emilmiş ve tozlu",
                delegate { AnimHost(AnimHostScene.Special); });
            AddAnim("Konak test: the passenger's identity (press to cycle jokers)",
                "konak testi: yolcunun kimliği (her basışta başka joker)",
                delegate { AnimHost(AnimHostScene.PassengerColours); });
            AddAnim("Konak: SEATING - the clasp locks on", "konak: KENETLENME - kenet oturuyor",
                delegate { AnimHost(AnimHostScene.Seating); });
            AddAnim("Konak: idle - the cube tries to get out", "konak: bekleme - küp çıkmaya çalışıyor",
                delegate { AnimHost(AnimHostScene.IdlePulse); });
            AddAnim("Konak: idle on OBSIDIAN (readability test)",
                "konak: OBSİDYEN üzerinde bekleme (okunurluk testi)",
                delegate { AnimHost(AnimHostScene.IdleObsidian); });
            AddAnim("Konak: line tear on OBSIDIAN", "konak: OBSİDYEN üzerinde hat yırtığı",
                delegate { AnimHost(AnimHostScene.TearObsidian); });
            AddAnim("Konak: a power tries to destroy it and is REFUSED",
                "konak: bir güç kırmayı deniyor ve REDDEDİLİYOR",
                delegate { AnimHost(AnimHostScene.ResistDestroy); });
            AddAnim("Konak: a moving board tries to carry it RIGHT",
                "konak: hareketli tahta onu SAĞA taşımaya çalışıyor",
                delegate { AnimHost(AnimHostScene.ForcedRight); });
            AddAnim("Konak: a moving board tries to carry it UP",
                "konak: hareketli tahta onu YUKARI taşımaya çalışıyor",
                delegate { AnimHost(AnimHostScene.ForcedUp); });
            AddAnim("Konak: the sweep passes over and cannot take it",
                "konak: temizlik üstünden geçiyor ama alamıyor",
                delegate { AnimHost(AnimHostScene.SweepPass); });
            AddAnim("Konak: a HORIZONTAL line takes it - bond severance",
                "konak: YATAY hat onu alıyor - bağ kopması",
                delegate { AnimHost(AnimHostScene.LineHorizontal); });
            AddAnim("Konak: a VERTICAL line takes it", "konak: DİKEY hat onu alıyor",
                delegate { AnimHost(AnimHostScene.LineVertical); });
            AddAnim("Konak: the passenger's own death beat",
                "konak: yolcunun kendi ölüm anı",
                delegate { AnimHost(AnimHostScene.PassengerLoss); });
            AddAnim("Konak: the WHOLE life - seat, resist, resist, die",
                "konak: TÜM yaşam - kenetlen, diren, diren, öl",
                delegate { AnimHost(AnimHostScene.Lifecycle); });
            AddAnim("Konak HERO: the whole life on OBSIDIAN",
                "konak HERO: OBSİDYEN üzerinde tüm yaşam",
                delegate { AnimHost(AnimHostScene.LifecycleObsidian); });
            AddAnim("Konak test: colour drain OFF (the film alone says nothing)",
                "konak testi: renk emme KAPALI (zar tek başına bir şey söylemiyor)",
                delegate { AnimHost(AnimHostScene.DrainNone); });
            AddAnim("Konak test: colour drain THIN", "konak testi: renk emme İNCE",
                delegate { AnimHost(AnimHostScene.DrainThin); });
            AddAnim("Konak test: colour drain MEDIUM (the tuned value)",
                "konak testi: renk emme ORTA (ayarlı değer)",
                delegate { AnimHost(AnimHostScene.DrainMedium); });
            AddAnim("Konak test: colour drain HEAVY", "konak testi: renk emme AĞIR",
                delegate { AnimHost(AnimHostScene.DrainHeavy); });
            AddAnim("Konak test: the MEMBRANE alone", "konak testi: yalnızca ZAR",
                delegate { AnimHost(AnimHostScene.MembraneOnly); });
            AddAnim("Konak test: the NECROTIC CRUST alone", "konak testi: yalnızca ÖLÜ KABUK",
                delegate { AnimHost(AnimHostScene.PatchesOnly); });
            AddAnim("Konak test: the WRAP FOLDS alone", "konak testi: yalnızca SARGI KIVRIMLARI",
                delegate { AnimHost(AnimHostScene.FoldsOnly); });
            AddAnim("Konak test: the NEST and its passenger alone",
                "konak testi: yalnızca YUVA ve yolcusu",
                delegate { AnimHost(AnimHostScene.NestOnly); });
            AddAnim("Konak switch: the binding strands",
                "konak anahtarı: sarma bantları",
                delegate { AnimHostToggle(ref ParasiteHostView.Layers.ShowRibs,
                    "ribs", "bantlar"); });
            AddAnim("Konak switch: the membrane",
                "konak anahtarı: zar",
                delegate { AnimHostToggle(ref ParasiteHostView.Layers.ShowMembrane,
                    "membrane", "zar"); });
            AddAnim("Konak switch: the thick wrap folds",
                "konak anahtarı: kalın sargı kıvrımları",
                delegate { AnimHostToggle(ref ParasiteHostView.Layers.ShowFolds,
                    "folds", "kıvrımlar"); });
            AddAnim("Konak switch: the film's holes (negative space)",
                "konak anahtarı: zarın delikleri (negatif alan)",
                delegate { AnimHostToggle(ref ParasiteHostView.Layers.ShowWindows,
                    "windows", "delikler"); });
            AddAnim("Konak switch: the necrotic crust",
                "konak anahtarı: ölü kabuk",
                delegate { AnimHostToggle(ref ParasiteHostView.Layers.ShowPatches,
                    "crust", "kabuk"); });
            AddAnim("Konak switch: the veins", "konak anahtarı: damarlar",
                delegate { AnimHostToggle(ref ParasiteHostView.Layers.ShowVeins,
                    "veins", "damarlar"); });
            AddAnim("Konak switch: the nest core", "konak anahtarı: yuva çekirdeği",
                delegate { AnimHostToggle(ref ParasiteHostView.Layers.ShowCore,
                    "core", "çekirdek"); });
            AddAnim("Konak switch: the passenger's essence",
                "konak anahtarı: yolcunun özü",
                delegate { AnimHostToggle(ref ParasiteHostView.Layers.ShowEssence,
                    "essence", "öz"); });
            AddAnim("Konak switch: the membrane's deformation (the cube pushing out)",
                "konak anahtarı: zarın deformasyonu (kübün dışarı bastırması)",
                delegate { AnimHostToggle(ref ParasiteHostView.Layers.ShowDeformation,
                    "deformation", "deformasyon"); });
            AddAnim("Konak switch: the harness's contact shadows",
                "konak anahtarı: kenedin temas gölgeleri",
                delegate { AnimHostToggle(ref ParasiteHostView.Layers.ShowShadows,
                    "shadows", "gölgeler"); });
            AddAnim("Konak switch: ALL parasite layers back on",
                "konak anahtarı: TÜM parazit katmanları geri açık",
                delegate
                {
                    ParasiteHostView.Layers.AllOn();
                    animLastLabel = Loc.Pick(
                        "every parasite layer on, drain back to its tuned value",
                        "tüm parazit katmanları açık, emme ayarlı değerine döndü");
                    if (AnimLabOpen)
                    {
                        RedrawAnimationLab();
                    }
                });
            AddAnimHeader("mapus / mühürlü hücre", "mapus / mühürlü hücre");
            // "MAPUS" - the prison it builds in one empty cell, on a board of the lab's own with
            // the REAL rules run on it: the boss picks its own target through MapusBoss.Retarget,
            // and the scene plays whatever it reported. Which cell is sealed, whether the seal
            // moved or held, and how close the lines are to completion are never fabricated here.
            AddAnim("Mapus: the seal, standing", "mapus: mühür, duruyor",
                delegate { AnimMapus(AnimMapusScene.Standing); });
            AddAnim("Mapus: the seal on an EMPTY board", "mapus: BOŞ tahtada mühür",
                delegate { AnimMapus(AnimMapusScene.EmptyBoard); });
            AddAnim("Mapus: the seal on a BUSY board", "mapus: DOLU tahtada mühür",
                delegate { AnimMapus(AnimMapusScene.BusyBoard); });
            AddAnim("Mapus: CELL SENTENCE - the prison goes up",
                "mapus: HÜCRE CEZASI - hapishane kuruluyor",
                delegate { AnimMapus(AnimMapusScene.Spawn); });
            AddAnim("Mapus: CELL RELEASE - the prison comes down",
                "mapus: TAHLİYE - hapishane sökülüyor",
                delegate { AnimMapus(AnimMapusScene.Despawn); });
            AddAnim("Mapus: the seal MOVES to another cell",
                "mapus: mühür başka hücreye TAŞINIYOR",
                delegate { AnimMapus(AnimMapusScene.Move); });
            AddAnim("Mapus: it HOLDS the same cell for three turns",
                "mapus: aynı hücreyi üç tur TUTUYOR",
                delegate { AnimMapus(AnimMapusScene.HoldsThreeTurns); });
            AddAnim("Mapus HERO: the cap RELEASES the cell - the player's window",
                "mapus HERO: sınır hücreyi BIRAKIYOR - oyuncunun penceresi",
                delegate { AnimMapus(AnimMapusScene.CapRelease); });
            AddAnim("Mapus HERO: the row is held by THIS cell alone",
                "mapus HERO: satırı tam da BU hücre tutuyor",
                delegate { AnimMapus(AnimMapusScene.RowHeldAlone); });
            AddAnim("Mapus HERO: the column is held by this cell alone",
                "mapus HERO: sütunu tam da bu hücre tutuyor",
                delegate { AnimMapus(AnimMapusScene.ColumnHeldAlone); });
            AddAnim("Mapus HERO: a row AND a column, both held",
                "mapus HERO: hem satır hem sütun, ikisi de tutulu",
                delegate { AnimMapus(AnimMapusScene.BothHeldAlone); });
            AddAnim("Mapus: WARDEN CHECK - the idle", "mapus: GARDİYAN KONTROLÜ - bekleme",
                delegate { AnimMapus(AnimMapusScene.WardenCheck); });
            AddAnim("Mapus: the look down the well (the rarer idle)",
                "mapus: kuyunun dibine bakış (seyrek bekleme)",
                delegate { AnimMapus(AnimMapusScene.DepthIdle); });
            AddAnim("Mapus: DENIED ENTRY - a block is dragged over it",
                "mapus: GİRİŞ YOK - üstüne blok sürükleniyor",
                delegate { AnimMapus(AnimMapusScene.Denied); });
            AddAnim("Mapus: a WHOLE turn - hold, move, lock, idle",
                "mapus: TÜM tur - tut, taşı, kilitle, bekle",
                delegate { AnimMapus(AnimMapusScene.WholeTurn); });
            AddAnim("Mapus test: readability over many coloured cubes",
                "mapus testi: bir sürü renkli küpün arasında okunurluk",
                delegate { AnimMapus(AnimMapusScene.ColourStress); });
            AddAnim("Mapus switch: the pit", "mapus anahtarı: çukur",
                delegate { AnimMapusToggle(ref MapusSealView.Layers.ShowPit, "pit", "çukur"); });
            AddAnim("Mapus switch: the edge sockets", "mapus anahtarı: kenar yuvaları",
                delegate { AnimMapusToggle(ref MapusSealView.Layers.ShowSockets,
                    "sockets", "yuvalar"); });
            AddAnim("Mapus switch: the warden ribs", "mapus anahtarı: gardiyan kaburgaları",
                delegate { AnimMapusToggle(ref MapusSealView.Layers.ShowRibs,
                    "ribs", "kaburgalar"); });
            AddAnim("Mapus switch: the warden seal", "mapus anahtarı: mühür",
                delegate { AnimMapusToggle(ref MapusSealView.Layers.ShowSeal, "seal", "mühür"); });
            AddAnim("Mapus switch: the brand pressed into it",
                "mapus anahtarı: mühre basılı damga",
                delegate { AnimMapusToggle(ref MapusSealView.Layers.ShowBrand,
                    "brand", "damga"); });
            AddAnim("Mapus switch: the ROW pressure", "mapus anahtarı: SATIR baskısı",
                delegate { AnimMapusToggle(ref MapusSealView.Layers.ShowRowPressure,
                    "row pressure", "satır baskısı"); });
            AddAnim("Mapus switch: the COLUMN pressure", "mapus anahtarı: SÜTUN baskısı",
                delegate { AnimMapusToggle(ref MapusSealView.Layers.ShowColumnPressure,
                    "column pressure", "sütun baskısı"); });
            AddAnim("Mapus switch: the seal's warmth", "mapus anahtarı: mührün sıcaklığı",
                delegate { AnimMapusToggle(ref MapusSealView.Layers.ShowWarmth,
                    "warmth", "sıcaklık"); });
            AddAnim("Mapus TEST: the PROPELLER test - silhouette only",
                "mapus TESTİ: PERVANE testi - yalnızca siluet",
                delegate { AnimMapusToggle(ref MapusSealView.Layers.SilhouetteTest,
                    "silhouette test", "siluet testi"); });
            AddAnim("Mapus TEST: EDGE MASS - housing green, shaft blue, head red",
                "mapus TESTİ: KENAR KÜTLESİ - yuva yeşil, gövde mavi, baş kırmızı",
                delegate { AnimMapusToggle(ref MapusSealView.Layers.MassTest,
                    "mass test", "kütle testi"); });
            AddAnim("Mapus switch: ALL layers back on",
                "mapus anahtarı: TÜM katmanlar geri açık",
                delegate
                {
                    MapusSealView.Layers.AllOn();
                    animLastLabel = Loc.Pick("every mapus layer on",
                        "tüm mapus katmanları açık");
                    if (AnimLabOpen)
                    {
                        RedrawAnimationLab();
                    }
                });
            AddAnimHeader("raw - not reworked yet", "ham - henüz elden geçirilmedi");
            // Everything under this heading is the CURRENT state of something nobody has designed
            // yet: a mechanic with no visual language of its own, or a look that exists in the game
            // but had no way of being reached from here. They are deliberately plain - the point is
            // to have a BEFORE to hold the next pass against.
            // Mapus has a section of its own now; what is left raw here is "Tılsım"'s bonus
            // ground, which is still nothing but a colour.
            AddAnim("RAW - Tılsım bonus ground (a tint, nothing else)",
                "ham - Tılsım bonus zemini (yalnız renk)",
                delegate { AnimRawBoard(AnimRawScene.CellStates); });
            AddAnim("RAW - overtime: the board's pressure squeeze (overtime knob)",
                "ham - uzatma: alanın basınç sıkışması (uzatma ayarı)", AnimRawPressure);
            AddAnim("RAW - overtime: the screen vignette (overtime knob)",
                "ham - uzatma: ekran vinyeti (uzatma ayarı)", AnimRawVignette);
            AddAnim("RAW - overtime: the glow under the grid (overtime knob)",
                "ham - uzatma: ızgara altındaki hat parıltısı (uzatma ayarı)", AnimRawGlow);
            AddAnim("RAW - Öteki dünya: two boards on one screen",
                "ham - Öteki dünya: tek ekranda iki tahta", AnimRawMirror);
            AddAnim("RAW - the backdrop alone (everything else hidden for a beat)",
                "ham - yalnız arka plan (gerisi bir an için gizlenir)", AnimRawBackdrop);
            AddAnim("RAW - retro skin: CRT scanlines + bit crush on/off",
                "ham - retro deri: CRT tarama çizgileri + bit-crush aç/kapa", AnimRawRetro);
            AddAnim("RAW - a card in the hand: lift, drag, drop back",
                "ham - eldeki kart: kaldır, sürükle, yerine bırak", AnimRawCardFeel);
            AddAnim("RAW - Devre cooking, slowed right down (heat, cracks, ash)",
                "ham - Devre pişmesi, iyice yavaşlatılmış (ısı, çatlak, kül)", AnimRawCircuitSlow);
            AddAnimHeader("full sequences", "tam diziler");
            AddAnim("TURN: line clear", "TUR: satır patlaması", AnimTurnLineClear);
            AddAnim("TURN: clean sweep", "TUR: temizlik", AnimTurnCleanSweep);
            AddAnim("TURN: dynamite board clear", "TUR: dinamit alan temizliği", AnimTurnDynamite);
            AddAnim("TURN: water -> boom -> water", "TUR: su -> patlama -> su", AnimTurnWaterBoom);
            AddAnim("round-start presentation", "raunt başı sunumu",
                delegate { StartRoundPresentation(); });
        }

        // ------------------------------------------------------------------ synthesis helpers
        //
        // Everything below fabricates ARGUMENTS for the real animation methods. None of it
        // reads or writes game rules.

        private GameBoard AnimBoard()
        {
            return boardView != null ? boardView.Board : null;
        }

        private BlockElement AnimElement()
        {
            return AnimElements[animElementIndex];
        }

        private int AnimCellCount()
        {
            return AnimCellCounts[animCellsIndex];
        }

        /// <summary>The N cells nearest the middle of the board, N from the cells knob. Centred
        /// so a blast reads as one event rather than a scatter along an edge.</summary>
        /// <summary>A REAL circuit path for the lab: top edge to bottom edge, one contiguous
        /// horizontal run per row, consecutive runs touching. AnimCells was being used for this
        /// and it returns the cells NEAREST THE MIDDLE - a diamond, which the joker could never
        /// produce: its paths are monotone along one axis and can never double back, so a plus
        /// is not a shape a circuit can take. Deterministic, so the lab shows the same route
        /// every time and a change to the drawing is the only thing that can move.</summary>
        private List<GridPos> AnimCircuitPath()
        {
            var cells = new List<GridPos>();
            GameBoard board = AnimBoard();
            if (board == null)
            {
                return cells;
            }
            // How far the run wanders left or right on each row down the board.
            int[] steps = { 0, 2, -1, 3, -2, 1, -3, 2, -1, 2, -2, 1 };
            int x = board.MinX + board.Width / 2;
            for (int row = 0; row < board.Height; row++)
            {
                int y = board.MinY + board.Height - 1 - row;   // top edge downward
                int next = Mathf.Clamp(x + steps[row % steps.Length],
                    board.MinX, board.MinX + board.Width - 1);
                int step = next >= x ? 1 : -1;
                for (int cx = x; ; cx += step)
                {
                    var pos = new GridPos(cx, y);
                    if (board.IsInside(pos))
                    {
                        cells.Add(pos);
                    }
                    if (cx == next)
                    {
                        break;
                    }
                }
                x = next;
            }
            return cells;
        }

        private List<GridPos> AnimCells()
        {
            return AnimCells(AnimCellCount());
        }

        /// <summary>
        /// N cells spread across the WHOLE arena instead of packed in its middle - a power or a
        /// "Hedefli" payout lands wherever it lands, and the burst still has to read as one event
        /// across the gaps. Farthest-point picking: the middle first, then always the cell
        /// furthest from everything taken so far, so the spread is even and the same every time.
        /// </summary>
        private List<GridPos> AnimScatteredCells()
        {
            List<GridPos> all = AnimCells(int.MaxValue);
            int want = Mathf.Min(AnimCellCount(), all.Count);
            var cells = new List<GridPos>(want);
            if (want == 0)
            {
                return cells;
            }
            var gap = new float[all.Count];
            for (int i = 0; i < all.Count; i++)
            {
                gap[i] = float.MaxValue;
            }
            int next = 0;
            for (int n = 0; n < want; n++)
            {
                GridPos taken = all[next];
                cells.Add(taken);
                gap[next] = -1f;
                int best = -1;
                float bestGap = -1f;
                for (int i = 0; i < all.Count; i++)
                {
                    if (gap[i] < 0f)
                    {
                        continue;
                    }
                    float dx = all[i].X - taken.X;
                    float dy = all[i].Y - taken.Y;
                    gap[i] = Mathf.Min(gap[i], dx * dx + dy * dy);
                    if (gap[i] > bestGap)
                    {
                        bestGap = gap[i];
                        best = i;
                    }
                }
                if (best < 0)
                {
                    break;
                }
                next = best;
            }
            return cells;
        }

        /// <summary>The <paramref name="count"/> cells nearest the middle of the board, nearest
        /// first.</summary>
        private List<GridPos> AnimCells(int count)
        {
            var cells = new List<GridPos>();
            GameBoard board = AnimBoard();
            if (board == null)
            {
                return cells;
            }
            float cx = board.MinX + (board.Width - 1) * 0.5f;
            float cy = board.MinY + (board.Height - 1) * 0.5f;
            var ranked = new List<KeyValuePair<float, GridPos>>();
            for (int x = board.MinX; x < board.MinX + board.Width; x++)
            {
                for (int y = board.MinY; y < board.MinY + board.Height; y++)
                {
                    var pos = new GridPos(x, y);
                    if (!board.IsInside(pos))
                    {
                        continue;
                    }
                    float dx = x - cx;
                    float dy = y - cy;
                    ranked.Add(new KeyValuePair<float, GridPos>(dx * dx + dy * dy, pos));
                }
            }
            ranked.Sort(delegate (KeyValuePair<float, GridPos> a, KeyValuePair<float, GridPos> b)
            {
                return a.Key.CompareTo(b.Key);
            });
            int want = Mathf.Min(count, ranked.Count);
            for (int i = 0; i < want; i++)
            {
                cells.Add(ranked[i].Value);
            }
            return cells;
        }

        private int AnimMiddleRow()
        {
            GameBoard board = AnimBoard();
            return board != null ? board.MinY + board.Height / 2 : 0;
        }

        private int AnimMiddleColumn()
        {
            GameBoard board = AnimBoard();
            return board != null ? board.MinX + board.Width / 2 : 0;
        }

        /// <summary>The zone the lab is showing, in board coordinates.</summary>
        private readonly List<GridPos> animQuarantineCells = new List<GridPos>();

        /// <summary>
        /// Relays the zone one cell larger every time it is pressed, exactly the way the boss
        /// does it: a fresh scattered patch, never the old one extended. Pressing it repeatedly
        /// is the only way to see what this effect is actually FOR - the seal blooming out of
        /// each new cell, cells that have LEFT the zone lifting, and separate patches merging
        /// into one field where they happen to touch.
        ///
        /// The cells are walked in a fixed stride rather than drawn at random, so the lab is
        /// repeatable frame to frame - the game's own zone is rng, which is not something to
        /// study an animation through.
        /// </summary>
        private void AnimQuarantineSeal()
        {
            GameBoard board = AnimBoard();
            if (board == null)
            {
                return;
            }
            var playable = new List<GridPos>();
            for (int y = board.MinY; y < board.MinY + board.Height; y++)
            {
                for (int x = board.MinX; x < board.MinX + board.Width; x++)
                {
                    var cell = new GridPos(x, y);
                    if (board.IsInside(cell)) { playable.Add(cell); }
                }
            }
            int want = Mathf.Min(animQuarantineCells.Count + 1, playable.Count / 2);
            // A stride that shares no factor with the board width scatters the patch instead of
            // laying it out in a line, and shifting the start each press relays it somewhere else.
            int start = animQuarantineCells.Count * 3;
            animQuarantineCells.Clear();
            for (int i = 0; i < want && playable.Count > 0; i++)
            {
                animQuarantineCells.Add(playable[(start + i * 5) % playable.Count]);
            }
            boardView.ShowQuarantine(animQuarantineCells);
        }

        /// <summary>
        /// The whole of "Devre" breaking, both halves of it, because in the game they are one
        /// event: the cubes standing on the circuit go off in the cable's own colour, and the
        /// cable overloads along its length. Both calls are the ones the game makes from
        /// EmitBlastParticles and RefreshCircuit - only the cell list is fabricated.
        ///
        /// Draw "devre izi" first: the overload reads its route off the live trace, exactly as
        /// it does in a real turn, so there has to be a cable there to break.
        /// </summary>
        private void AnimCircuitBreak()
        {
            IReadOnlyList<GridPos> path = AnimCircuitPath();
            boardView.DetonateCircuit();
            boardView.PlayCircuitHeat(AnimCircuitCubes(path),
                CircuitOverloadView.RuptureTime);
        }

        /// <summary>The cubes the break would take. Whatever is really standing on those cells,
        /// and a plain one where the board is empty - the lab has to be able to show the heat
        /// without the player first having to fill a circuit by hand.</summary>
        private List<DestroyedCube> AnimCircuitCubes(IReadOnlyList<GridPos> path)
        {
            var cubes = new List<DestroyedCube>();
            GameBoard board = AnimBoard();
            for (int i = 0; i < path.Count; i++)
            {
                Cube? real = board != null ? board.GetCube(path[i]) : null;
                // A card id that actually RESOLVES. Id 0 does not: BoardView.CardOf fails to
                // find it, counts it unresolved and draws the debug outline - which is what those
                // red hollow squares were, not a block design.
                cubes.Add(new DestroyedCube(path[i],
                    real ?? new Cube(CubeKind.Normal, AnimCardId())));
            }
            return cubes;
        }

        /// <summary>A real card out of the player's own deck, so a fabricated cube is drawn with
        /// the tile and colour that card actually gives it.</summary>
        private int AnimCardId()
        {
            IReadOnlyList<BlockCard> owned = session != null ? session.OwnedCards : null;
            return owned != null && owned.Count > 0 ? owned[0].Id : 0;
        }

        private int animInvaderTurns = 3;

        /// <summary>The cubes a column extraction would take. Whatever really stands there, and a
        /// plain one where the board is empty, so the lab can show the sweep without the player
        /// first having to fill a column by hand.</summary>
        private List<DestroyedCube> AnimColumnCubes(int column)
        {
            var cubes = new List<DestroyedCube>();
            GameBoard board = AnimBoard();
            if (board == null)
            {
                return cubes;
            }
            for (int y = board.MinY; y < board.MinY + board.Height; y++)
            {
                var cell = new GridPos(column, y);
                Cube? real = board.GetCube(cell);
                cubes.Add(new DestroyedCube(cell, real ?? new Cube(CubeKind.Normal, AnimCardId())));
            }
            return cubes;
        }

        /// <summary>Which case the collection entry shows next (see AnimColumnSweep).</summary>
        private int animSweepCase;

        /// <summary>
        /// THE COLLECTION, one case per press. These are the five that can actually go wrong, and
        /// each of them checks something different:
        ///
        ///   EMPTY    the band has to sweep a bare column cleanly - nothing to take is a case,
        ///            not an absence of one.
        ///   ONE      one cube alone, where the capture and the stretch are large enough to read
        ///            frame by frame.
        ///   FULL     the whole column, for the RHYTHM: scan, take, scan, take.
        ///   COLOURS  four different blocks, because each one has to leave in its OWN colour -
        ///            if the sweep turns them all amber, this is where it shows.
        ///   HARD     obsidian and gold. Nothing resists this, and they must go the same way as
        ///            everything else: no cracking for the obsidian, no melting for the gold.
        /// </summary>
        private void AnimColumnSweep()
        {
            int column = AnimMiddleColumn();
            boardView.ShowDoomedColumn(column, 1);
            boardView.PlayColumnExtraction(AnimSweepCubes(column, animSweepCase));
            animSweepCase = (animSweepCase + 1) % 5;
        }

        private List<DestroyedCube> AnimSweepCubes(int column, int which)
        {
            var cubes = new List<DestroyedCube>();
            GameBoard board = AnimBoard();
            if (board == null)
            {
                return cubes;
            }
            int height = board.Height;
            int middle = board.MinY + height / 2;
            var mixed = new[] { CubeKind.Normal, CubeKind.Fire, CubeKind.Water, CubeKind.Normal };
            for (int y = board.MinY; y < board.MinY + height; y++)
            {
                var cell = new GridPos(column, y);
                switch (which)
                {
                    case 0:
                        continue;                                   // EMPTY: nothing to take
                    case 1:
                        if (y != middle)
                        {
                            continue;                               // ONE
                        }
                        cubes.Add(new DestroyedCube(cell,
                            new Cube(CubeKind.Normal, AnimCardId())));
                        break;
                    case 2:
                        cubes.Add(new DestroyedCube(cell,
                            new Cube(CubeKind.Normal, AnimCardId())));   // FULL
                        break;
                    case 3:
                        cubes.Add(new DestroyedCube(cell,               // COLOURS
                            new Cube(mixed[(y - board.MinY) % mixed.Length], AnimCardId())));
                        break;
                    default:
                        cubes.Add(new DestroyedCube(cell,               // HARD
                            new Cube((y - board.MinY) % 2 == 0
                                ? CubeKind.Obsidian : CubeKind.Gold, AnimCardId())));
                        break;
                }
            }
            return cubes;
        }

        private List<int> AnimLines(bool rows)
        {
            var list = new List<int>();
            list.Add(rows ? AnimMiddleRow() : AnimMiddleColumn());
            return list;
        }

        /// <summary>A small L-shaped scratch block carrying the element knob's element, so any
        /// animation that needs "a card" gets a plausible one.</summary>
        private BlockCard AnimScratchCard()
        {
            return AnimScratchCard(AnimElement());
        }

        /// <summary>The same scratch block with a chosen element - or none, for a plain block,
        /// which the element knob has no setting for.</summary>
        private BlockCard AnimScratchCard(BlockElement? element)
        {
            var cells = new List<GridPos>
            {
                new GridPos(0, 0), new GridPos(0, 1), new GridPos(1, 0)
            };
            BlockShape shape = BlockShape.FromCells(cells);
            // "Hedefli" marks ONE cube, and the index saying which is set only by the rules when a
            // card is minted - out of the view's reach. A designed block with the target on its
            // first cube and plain cubes after it is drawn the very same way: the bullseye on one,
            // the targeted body on the rest. Anything else falls as a targeted block with no target.
            if (element == BlockElement.Targeted)
            {
                return BlockCard.Designed(-4242, shape,
                    new BlockElement?[] { BlockElement.Targeted, null, null });
            }
            var elements = new List<BlockElement>();
            if (element.HasValue)
            {
                elements.Add(element.Value);
            }
            // A negative id keeps it clear of every real card in the run; nothing in the View
            // looks a card up by id, so this only ever picks its colour and its label.
            return new BlockCard(-4242, shape, elements);
        }

        private void AnimCards(CardLayerView.DebugAnim which)
        {
            cardLayer.PlayDebugAnimation(which, session.CurrentRound);
        }

        // ------------------------------------------------------------------ entry bodies

        private void AnimReplaceCard()
        {
            RoundEngine round = session.CurrentRound;
            if (round != null && round.Hand.Count > 0)
            {
                cardLayer.AnimateReplaceCard(round, round.Hand[0].Id);
            }
        }

        private void AnimRevealBeat()
        {
            RoundEngine round = session.CurrentRound;
            if (round == null)
            {
                return;
            }
            var cards = new List<BlockCard>();
            for (int i = 0; i < round.Hand.Count; i++)
            {
                cards.Add(round.Hand[i]);
            }
            cardLayer.ShowRevealBeat(cards, ShellGameRevealSeconds);
        }

        private List<BlockShape> AnimDemandShapes()
        {
            var shapes = new List<BlockShape>();
            shapes.Add(BlockShape.FromCells(new List<GridPos>
            {
                new GridPos(0, 0), new GridPos(1, 0)
            }));
            shapes.Add(BlockShape.FromCells(new List<GridPos>
            {
                new GridPos(0, 0), new GridPos(0, 1), new GridPos(1, 1)
            }));
            shapes.Add(BlockShape.FromCells(new List<GridPos>
            {
                new GridPos(0, 0), new GridPos(1, 0), new GridPos(0, 1), new GridPos(1, 1)
            }));
            return shapes;
        }

        /// <summary>Synthetic fall frames: a few cubes stepping along the arena's OWN WaterFlow,
        /// so the lab shows the gravity the round is actually under ("Kütleçekim merkezi").</summary>
        private List<IReadOnlyList<WaterMove>> AnimWaterFrames(int steps)
        {
            var frames = new List<IReadOnlyList<WaterMove>>();
            GameBoard board = AnimBoard();
            if (board == null)
            {
                return frames;
            }
            GridPos flow = board.WaterFlow;
            int maxX = board.MinX + board.Width - 1;
            int maxY = board.MinY + board.Height - 1;
            // Start at the far edge the flow runs AWAY from, and lay the cubes out across it.
            var starts = new List<GridPos>();
            int lanes = Mathf.Min(3, flow.X != 0 ? board.Height : board.Width);
            for (int i = 0; i < lanes; i++)
            {
                if (flow.X != 0)
                {
                    int y = board.MinY + (board.Height * (i + 1)) / (lanes + 1);
                    starts.Add(new GridPos(flow.X < 0 ? maxX : board.MinX, y));
                }
                else
                {
                    int x = board.MinX + (board.Width * (i + 1)) / (lanes + 1);
                    starts.Add(new GridPos(x, flow.Y < 0 ? maxY : board.MinY));
                }
            }
            var at = new List<GridPos>(starts);
            for (int step = 0; step < steps; step++)
            {
                var frame = new List<WaterMove>();
                for (int i = 0; i < at.Count; i++)
                {
                    var next = new GridPos(at[i].X + flow.X, at[i].Y + flow.Y);
                    if (!board.IsInside(next))
                    {
                        continue;
                    }
                    frame.Add(new WaterMove(at[i], next));
                    at[i] = next;
                }
                if (frame.Count == 0)
                {
                    break;
                }
                frames.Add(frame);
            }
            return frames;
        }

        private void AnimWaterFall()
        {
            List<IReadOnlyList<WaterMove>> frames = AnimWaterFrames(6);
            if (frames.Count == 0)
            {
                return;
            }
            boardView.PlayWaterAnimation(frames, null);
        }

        private void AnimPreview(bool valid)
        {
            BlockShape shape = AnimScratchCard().Shape;
            var origin = new GridPos(AnimMiddleColumn(), AnimMiddleRow());
            AnimHoldPreview(delegate { boardView.ShowPreview(shape, origin, valid); });
        }

        /// <summary>Shows a static preview for long enough that its own pulse is visible, then
        /// clears it. The pulse lives in BoardView.Update, so holding IS the animation.</summary>
        private void AnimHoldPreview(System.Action show)
        {
            StartCoroutine(HoldPreviewRoutine(show));
        }

        private IEnumerator HoldPreviewRoutine(System.Action show)
        {
            show();
            yield return new WaitForSeconds(AnimHoldSeconds);
            boardView.ClearPreview();
        }

        private void AnimFallingPiece()
        {
            StartCoroutine(FallingPieceRoutine());
        }

        private IEnumerator FallingPieceRoutine()
        {
            GameBoard board = AnimBoard();
            if (board == null)
            {
                yield break;
            }
            BlockShape shape = AnimScratchCard().Shape;
            int x = AnimMiddleColumn();
            var ghost = new GridPos(x, board.MinY);
            int top = board.MinY + board.Height;
            for (int y = top; y >= board.MinY; y--)
            {
                boardView.ShowFallingPiece(shape, new GridPos(x, y), ghost);
                yield return new WaitForSeconds(0.14f);
            }
            boardView.ClearPreview();
        }

        /// <summary>
        /// The infection detonation, through the very call a turn makes - PlayInfectionBlock -
        /// with only its ARGUMENTS fabricated.
        ///
        /// A real turn arrives with the infection already on the board (RefreshAll put it there),
        /// so this puts it there first: the source cell, and then - in a SECOND call, the way a
        /// turn delivers it - the cells the spread took, which is what makes the core view treat
        /// them as plus-spread seeds rather than as first infections.
        ///
        /// The spread list is built the way the rules build it: the four orthogonal neighbours,
        /// minus any off the board (the rules also turn down an already-infected cell, and
        /// nothing else is infected here). The block's own cells are NOT turned down - they are
        /// empty by the time the spread runs - so an arm into the block's own ground is what a
        /// real first detonation does too.
        ///
        /// <paramref name="cells"/> of 0 means "use the cell-count knob".
        /// </summary>
        private void AnimInfectionBurst(int cells, bool spread)
        {
            GameBoard board = AnimBoard();
            List<GridPos> block = AnimCells();
            if (board == null || block.Count == 0)
            {
                return;
            }
            if (cells > 0 && block.Count > cells)
            {
                block.RemoveRange(cells, block.Count - cells);
            }
            // A lab block has no card behind it, so it wears the default tile - which is what a
            // plain block wears on the board too.
            var destroyed = new List<DestroyedCube>(block.Count);
            for (int i = 0; i < block.Count; i++)
            {
                destroyed.Add(new DestroyedCube(block[i], new Cube(CubeKind.Normal, -1)));
            }
            GridPos from = block[0];
            const int Threshold = 3;
            var marks = new List<InfectedCell> { new InfectedCell(from, 0, Threshold) };
            boardView.ShowInfections(marks);
            List<GridPos> arms = null;
            if (spread)
            {
                arms = new List<GridPos>();
                GridPos[] around =
                {
                    new GridPos(from.X + 1, from.Y), new GridPos(from.X - 1, from.Y),
                    new GridPos(from.X, from.Y + 1), new GridPos(from.X, from.Y - 1)
                };
                for (int i = 0; i < around.Length; i++)
                {
                    if (board.IsInside(around[i]))
                    {
                        arms.Add(around[i]);
                        marks.Add(new InfectedCell(around[i], 0, Threshold));
                    }
                }
                boardView.ShowInfections(marks);
            }
            float charge = boardView.PlayInfectionCharge(from);
            PlayInfectionBlock(destroyed, from, arms, charge * InfectionChargeDelay, true);
        }

        private void AnimInfectionPips()
        {
            const int Threshold = 3;
            int turns = Mathf.RoundToInt(Threshold * AnimInfectPercents[animInfectIndex] / 100f);
            var marks = new List<InfectedCell>();
            List<GridPos> cells = AnimCells();
            for (int i = 0; i < cells.Count; i++)
            {
                marks.Add(new InfectedCell(cells[i], turns, Threshold));
            }
            boardView.ShowInfections(marks);
        }

        // ------------------------------------------------------------------ Matruşka
        //
        // Every entry builds the dolls a real turn would have found, the events a real turn would have
        // reported and where the dolls would have ended up - then hands them to the same Hold/Release
        // the turn uses. Children go to cells well apart, so every arc can be followed. The lab's
        // status line shows what was staged: parent generation and cell, children and their cells.

        /// <summary>The generations the real boss has, so the lab's dolls wear the art they would.</summary>
        private const int AnimDollGenerations = 4;

        private GridPos AnimDollCell(int dx, int dy)
        {
            GameBoard board = AnimBoard();
            if (board == null)
            {
                return new GridPos(dx, dy);
            }
            return new GridPos(
                Mathf.Clamp(AnimMiddleColumn() + dx, board.MinX, board.MinX + board.Width - 1),
                Mathf.Clamp(AnimMiddleRow() + dy, board.MinY, board.MinY + board.Height - 1));
        }

        /// <summary>Shows the dolls a turn starts from, then stages that turn exactly as a real one is.</summary>
        private void AnimDollTurn(List<GridPos> beforeCells, List<int> beforeGenerations,
            List<DollEvent> events, List<GridPos> afterCells, List<int> afterGenerations, string debug)
        {
            boardView.ShowDolls(beforeCells, beforeGenerations, AnimDollGenerations);
            boardView.HoldDolls(events, afterCells, afterGenerations, AnimDollGenerations);
            boardView.ReleaseDolls();
            animLastLabel = debug;
        }

        private static string AnimCellText(GridPos cell)
        {
            return "(" + cell.X + "," + cell.Y + ")";
        }

        private void AnimDollArrives()
        {
            GridPos cell = AnimDollCell(0, 0);
            AnimDollTurn(new List<GridPos>(), new List<int>(),
                new List<DollEvent> { DollEvent.Arrived(cell, 1, false) },
                new List<GridPos> { cell }, new List<int> { 1 },
                "g1 " + AnimCellText(cell));
        }

        private void AnimDollIdle()
        {
            var cells = new List<GridPos>
            {
                AnimDollCell(-3, 0), AnimDollCell(-1, 0), AnimDollCell(1, 0), AnimDollCell(3, 0)
            };
            boardView.ShowDolls(cells, new List<int> { 1, 2, 3, 4 }, AnimDollGenerations);
        }

        /// <summary>One doll of <paramref name="generation"/> opening, its two children sent to far
        /// corners of the arena in opposite directions.</summary>
        private void AnimDollSplit(int generation)
        {
            GridPos parent = AnimDollCell(0, 0);
            GridPos a = AnimDollCell(-2, 2);
            GridPos b = AnimDollCell(2, -2);
            AnimDollTurn(new List<GridPos> { parent }, new List<int> { generation },
                new List<DollEvent> { DollEvent.Split(parent, generation, new List<GridPos> { a, b }) },
                new List<GridPos> { a, b }, new List<int> { generation + 1, generation + 1 },
                "g" + generation + " " + AnimCellText(parent) + " -> g" + (generation + 1) + " "
                    + AnimCellText(a) + " " + AnimCellText(b));
        }

        private void AnimDollEmptied()
        {
            GridPos cell = AnimDollCell(0, 0);
            AnimDollTurn(new List<GridPos> { cell }, new List<int> { AnimDollGenerations },
                new List<DollEvent> { DollEvent.Emptied(cell, AnimDollGenerations) },
                new List<GridPos>(), new List<int>(),
                "g" + AnimDollGenerations + " " + AnimCellText(cell) + " -> (boş)");
        }

        /// <summary>Three dolls of three generations opening on the same turn, their six children crossing
        /// the arena - the case the crowd control exists for.</summary>
        private void AnimDollManySplits()
        {
            GridPos p1 = AnimDollCell(-2, 0);
            GridPos p2 = AnimDollCell(0, 0);
            GridPos p3 = AnimDollCell(2, 0);
            GridPos[] kids =
            {
                AnimDollCell(-3, 2), AnimDollCell(1, -2),
                AnimDollCell(0, 2), AnimDollCell(-2, -2),
                AnimDollCell(3, 2), AnimDollCell(2, -3)
            };
            AnimDollTurn(new List<GridPos> { p1, p2, p3 }, new List<int> { 1, 2, 3 },
                new List<DollEvent>
                {
                    DollEvent.Split(p1, 1, new List<GridPos> { kids[0], kids[1] }),
                    DollEvent.Split(p2, 2, new List<GridPos> { kids[2], kids[3] }),
                    DollEvent.Split(p3, 3, new List<GridPos> { kids[4], kids[5] })
                },
                new List<GridPos>(kids), new List<int> { 2, 2, 3, 3, 4, 4 },
                "g1 " + AnimCellText(p1) + " g2 " + AnimCellText(p2) + " g3 " + AnimCellText(p3)
                    + " -> 6 çocuk");
        }

        private void AnimDollCarried()
        {
            GridPos from = AnimDollCell(-1, 0);
            GridPos to = AnimDollCell(2, 1);
            AnimDollTurn(new List<GridPos> { from }, new List<int> { 2 },
                new List<DollEvent> { DollEvent.Moved(from, to, 2, false) },
                new List<GridPos> { to }, new List<int> { 2 },
                "g2 " + AnimCellText(from) + " su -> " + AnimCellText(to) + " (bölünme yok)");
        }

        private void AnimDollLast()
        {
            GridPos cell = AnimDollCell(0, 0);
            AnimDollTurn(new List<GridPos> { cell }, new List<int> { AnimDollGenerations },
                new List<DollEvent>
                {
                    DollEvent.Emptied(cell, AnimDollGenerations),
                    DollEvent.AllCracked()
                },
                new List<GridPos>(), new List<int>(),
                "g" + AnimDollGenerations + " " + AnimCellText(cell) + " -> son bebek, boss biter");
        }

        /// <summary>Every other cell of the arena, row by row from the top, so no two test dolls touch.</summary>
        private GridPos AnimDollRestCell(int index)
        {
            GameBoard board = AnimBoard();
            int columns = Mathf.Max(1, (board.Width + 1) / 2);
            int x = board.MinX + (index % columns) * 2;
            int y = board.MinY + board.Height - 1 - (index / columns) * 2;
            return new GridPos(x, Mathf.Max(board.MinY, y));
        }

        /// <summary>The idle test: one large, two medium, four small and eight tiny, all at rest - for
        /// watching the material and the light rather than any event.</summary>
        private void AnimDollLightMixed()
        {
            if (AnimBoard() == null)
            {
                return;
            }
            var cells = new List<GridPos>();
            var generations = new List<int>();
            int[] counts = { 1, 2, 4, 8 };
            int index = 0;
            for (int generation = 1; generation <= counts.Length; generation++)
            {
                for (int k = 0; k < counts[generation - 1]; k++)
                {
                    cells.Add(AnimDollRestCell(index++));
                    generations.Add(generation);
                }
            }
            boardView.ShowDolls(cells, generations, AnimDollGenerations);
            animLastLabel = AnimDollLayersText();
        }

        private void AnimDollLightTiny()
        {
            if (AnimBoard() == null)
            {
                return;
            }
            var cells = new List<GridPos>();
            var generations = new List<int>();
            for (int i = 0; i < 8; i++)
            {
                cells.Add(AnimDollRestCell(i));
                generations.Add(AnimDollGenerations);
            }
            boardView.ShowDolls(cells, generations, AnimDollGenerations);
            animLastLabel = AnimDollLayersText();
        }

        /// <summary>Switches one of the idle's layers. The switches last until the lab closes.</summary>
        private void AnimDollLayer(int layer)
        {
            switch (layer)
            {
                case 0: MatryoshkaView.Layers.BodyWarmth = !MatryoshkaView.Layers.BodyWarmth; break;
                case 1: MatryoshkaView.Layers.GoldResponse = !MatryoshkaView.Layers.GoldResponse; break;
                case 2: MatryoshkaView.Layers.LacquerSheen = !MatryoshkaView.Layers.LacquerSheen; break;
                case 3: MatryoshkaView.Layers.PresenceLight = !MatryoshkaView.Layers.PresenceLight; break;
                case 4: MatryoshkaView.Layers.RimLight = !MatryoshkaView.Layers.RimLight; break;
                default: MatryoshkaView.Layers.Motion = !MatryoshkaView.Layers.Motion; break;
            }
            animLastLabel = AnimDollLayersText();
        }

        private static string AnimDollLayersText()
        {
            return Loc.Pick("warmth ", "sıcaklık ") + OnOff(MatryoshkaView.Layers.BodyWarmth)
                + Loc.Pick("  gold ", "  altın ") + OnOff(MatryoshkaView.Layers.GoldResponse)
                + Loc.Pick("  sheen ", "  cila ") + OnOff(MatryoshkaView.Layers.LacquerSheen)
                + Loc.Pick("  presence ", "  zemin ") + OnOff(MatryoshkaView.Layers.PresenceLight)
                + Loc.Pick("  rim ", "  kenar ") + OnOff(MatryoshkaView.Layers.RimLight)
                + Loc.Pick("  motion ", "  hareket ") + OnOff(MatryoshkaView.Layers.Motion);
        }

        /// <summary>
        /// Changes a TINT-BASED marker and repaints.
        ///
        /// The board's overlays come in two kinds and they clear very differently. The pips and
        /// icons (infection, circuit, dolls, gravity) are marker OBJECTS, so setting them to null
        /// destroys them there and then. The quarantine wash, the creature patch and the doomed
        /// column are CELL TINTS instead - they are only state until BoardView.Refresh paints the
        /// cells - and Refresh does NOT run every frame (Update only pulses the element cubes and
        /// the infection pips). So without this, both showing and clearing one of those three did
        /// nothing visible until something else happened to repaint the board.
        /// </summary>
        private void AnimTintMarker(System.Action change)
        {
            change();
            boardView.Refresh();
        }

        /// <summary>
        /// Walks the gravity FIELD through all four directions, one per play, ending on the
        /// default. Cycling rather than showing the round's real flow, because the real flow is
        /// (0,-1) on nearly every board and ShowGravity draws NOTHING for it - so an entry that
        /// asked the board would have looked broken on every arena but a "Kütleçekim merkezi" one.
        ///
        /// What each press shows is a TRANSITION, not a state: the activation beat, two re-aims
        /// and the collapse back to down. See AnimGravityFlows for why they are in that order.
        /// </summary>
        private void AnimGravityField()
        {
            boardView.ShowGravity(AnimGravityFlows[animGravityStep]);
            animGravityStep = (animGravityStep + 1) % AnimGravityFlows.Length;
        }

        private void AnimClearMarkers()
        {
            // The marker-object overlays clear on their own...
            boardView.ShowInfections(null);
            boardView.StopInfectionBurst();
            StopAnimFallSequence();
            boardView.ShowCircuit(null);
            boardView.ClearCircuitBlocks();
            boardView.ShowDolls(null, null, 0);
            boardView.ShowGravity(new GridPos(0, -1)); // the default draws no field
            animGravityStep = 0;
            boardView.ClearPreview();
            // ...the three tinted ones need the repaint (see AnimTintMarker).
            animQuarantineCells.Clear();
            AnimTintMarker(delegate
            {
                boardView.ShowQuarantine(null);
                boardView.ShowCreature(null);
                boardView.ShowDoomedColumn(null, 0);
            });
        }

        private void AnimMineDance()
        {
            GameBoard board = AnimBoard();
            if (board == null)
            {
                return;
            }
            // THE STAGES, one per press, because the parts of this are polished separately:
            //
            //   1 step   the REVEAL and the CLOSE on their own - the cover lifting, the two
            //            beats on the mine, the hold, and the cover landing. No dance at all.
            //   2 steps  ONE hop, which is where the lift, the shadow separation, the easing
            //            and the landing can be read frame by frame (turn the lab's time scale
            //            down to 0.25x for this one).
            //   full     all twelve, for the TEMPO across the run.
            //   again    the same twelve as a RE-reveal, which holds the look shorter.
            //
            // The path is the lab's own - the real one comes from the boss - but its SHAPE is the
            // same: a walk the eye can follow, so what is being judged is the motion.
            // Stage 0 holds the mine OPEN so its own look can be judged without racing the
            // rest of the sequence; the others run the whole thing.
            if (animMineStage == 0)
            {
                mineShuffle.PreviewMine(boardView, board,
                    new GridPos(AnimMiddleColumn(), AnimMiddleRow()));
                animMineStage = 1;
                return;
            }
            var path = new List<GridPos>();
            int y = AnimMiddleRow();
            int steps = animMineStage == 1 ? 1 : animMineStage == 2 ? 2 : 12;
            for (int i = 0; i < steps; i++)
            {
                int x = board.MinX + (i * 3 + 1) % Mathf.Max(1, board.Width);
                path.Add(new GridPos(x, y));
            }
            mineShuffle.Play(boardView, board, path, animMineStage == 4);
            animMineStage = (animMineStage + 1) % 5;
        }

        /// <summary>Which part of the shell game the entry shows next (see AnimMineDance).</summary>
        private int animMineStage;

        /// <summary>The combo knob's reading, and WHICH tier it will actually DRAW when the two
        /// differ. All three tiers are painted now, so today it always reads as a plain number;
        /// it earns its place the next time a tier is designed before it is drawn, when several
        /// knob settings would otherwise play the same burst with nothing on screen saying why -
        /// which is exactly how you end up unable to tell whether a newly installed sheet is the
        /// one you are watching. Reads "4 -> 3" in that case.</summary>
        private string ComboKnobLabel()
        {
            int asked = Mathf.Clamp(animCombo, 1, LineBurstView.MaxTier);
            int drawn = LineBurstView.EffectiveTier(asked);
            return drawn == asked ? animCombo.ToString() : animCombo + " → " + drawn;
        }

        /// <summary>FlashLine at the tier the COMBO KNOB is on. The lab has no streak of its
        /// own to be at a tier of, so the knob stands in for one - which also makes it the way
        /// to look at a tier whose art has just landed. Clamped to the tiers that exist, so the
        /// knob's 0 and its 4-6 both still land on something drawn.</summary>
        private void FlashLineAtKnob(GameBoard board, int line, bool row)
        {
            activeLineTier = Mathf.Clamp(animCombo, 1, LineBurstView.MaxTier);
            FlashLine(board, line, row);
        }

        /// <summary>Both rays at once, which is how a turn that completes a row and a column at
        /// the same time reads: two waves leaving the same middle at the same instant.</summary>
        private void AnimPlusBlast()
        {
            FlashLineAtKnob(AnimBoard(), AnimMiddleRow(), true);
            FlashLineAtKnob(AnimBoard(), AnimMiddleColumn(), false);
        }

        /// <summary>
        /// A defective block falling through, via the same SpawnFallingCubes a turn calls.
        ///
        /// It drops a BLOCK - the scratch card's shape where a player would have put it, centred on
        /// the board - because that is what a turn hands over: report.FellThroughCells is the placed
        /// shape's own cells. It used to drop the cells nearest the middle, which is a blob no card
        /// has, so the lab showed a fall no block ever takes.
        /// </summary>
        private void AnimFallingCubes()
        {
            BlockCard card = AnimScratchCard();
            SpawnFallingCubes(boardView, AnimPlacedCells(card), card);
        }

        /// <summary>The cells a card would cover placed in the middle of the board - what a turn
        /// would report for it. Nothing is clipped to the board: a block falling out of the frame
        /// has no reason to lose a cube at the edge.</summary>
        private List<GridPos> AnimPlacedCells(BlockCard card)
        {
            var cells = new List<GridPos>();
            if (card == null)
            {
                return cells;
            }
            BlockShape shape = card.Shape;
            int ox = AnimMiddleColumn() - shape.Width / 2;
            int oy = AnimMiddleRow() - shape.Height / 2;
            for (int i = 0; i < shape.Cells.Count; i++)
            {
                cells.Add(new GridPos(ox + shape.Cells[i].X, oy + shape.Cells[i].Y));
            }
            return cells;
        }

        /// <summary>Flips one of the fold's debug switches and says which way it went. The next
        /// fold played shows the difference; closing the lab turns them all back on.</summary>
        private void AnimFoldToggle(ref bool flag, string english, string turkish)
        {
            flag = !flag;
            animLastLabel = Loc.Pick("phase fold " + english + ": ", "katlama " + turkish + ": ")
                + OnOff(flag);
            if (AnimLabOpen)
            {
                RedrawAnimationLab();
            }
        }

        /// <summary>The same for the sublimation's debug switches.</summary>
        private void AnimCryoToggle(ref bool flag, string english, string turkish)
        {
            flag = !flag;
            animLastLabel = Loc.Pick("cryo sublimation " + english + ": ", "süblimleşme " + turkish + ": ")
                + OnOff(flag);
            if (AnimLabOpen)
            {
                RedrawAnimationLab();
            }
        }

        // ------------------------------------------------------------------ boss lifts

        /// <summary>The two bosses that take cubes off by MOVING the board under them, so what they
        /// take is torn off along its step rather than removed where it stood.</summary>
        private enum AnimBossScene
        {
            Escalator,
            Centrifuge,
            /// <summary>The escalator on an arena with holes in its top row.</summary>
            Holes
        }

        /// <summary>The scene AnimBossLift has going, so pressing it again restarts it.</summary>
        private Coroutine animBossLift;

        /// <summary>How long the lab board stands before its first turn ends.</summary>
        private const float AnimBossBeat = 0.9f;

        /// <summary>Between the two turn ends, and after the second before the real board returns.</summary>
        private const float AnimBossTurnGap = 1.6f;

        /// <summary>
        /// One of those bosses AS IT PLAYS IN ITS ROUND - not a cold mark on cells in the middle of
        /// the arena, which no boss ever names. The lab puts up a board of its own, the real one's
        /// size and about half full of the run's own blocks, and ends two turns on it, each the
        /// game's own sequence: the board changes, it is repainted, and the move's own seams play on exactly
        /// the cells the boss reported.
        ///
        ///   ESCALATOR   the real ShiftRowsUp: every row rides up one (PlayBoardMoves) and the top
        ///               row's cubes are torn off over the top edge (PlayForcedExit) - both from what
        ///               the board code wrote.
        ///   CENTRIFUGE  the real FlingCubesOutward: every cube one cell further from the middle,
        ///               the rim's torn off outward - each along its own step, eight ways.
        ///   HOLES       the real ShiftRowsUp on an arena with holes in its top row: the cubes
        ///               under them ride in and have no ground; the top row's go over the edge.
        ///
        /// "Kangren" has nine scenes of its own (AnimRot) - it moves nothing, so it shares none of
        /// this machinery.
        ///
        /// The real board is never touched. AnimResync puts it back at the end - RefreshAll rebuilds
        /// whenever the view shows a board that is not the round's.
        /// </summary>
        private void AnimBossLift(AnimBossScene scene)
        {
            StopAnimBossLift();
            animBossLift = StartCoroutine(BossLiftRoutine(scene));
        }

        private void StopAnimBossLift()
        {
            if (animBossLift != null)
            {
                StopCoroutine(animBossLift);
                animBossLift = null;
            }
        }

        private IEnumerator BossLiftRoutine(AnimBossScene scene)
        {
            RoundEngine round = session != null ? session.CurrentRound : null;
            if (round == null || round.Board == null || boardView == null)
            {
                animBossLift = null;
                yield break;
            }
            // Never smaller than 6 x 6, so every scene has an inside, a rim and a line to kill.
            int w = Mathf.Max(6, round.Board.Width);
            int h = Mathf.Max(6, round.Board.Height);
            GameBoard board = scene == AnimBossScene.Holes
                ? new GameBoard(w, h - 1, AnimHoledTopRow(w, h))
                : new GameBoard(w, h);
            List<int> cards = AnimBossCards();
            for (int x = 0; x < board.Width; x++)
            {
                for (int y = 0; y < board.Height; y++)
                {
                    Cube? cube = AnimBossCell(scene, board.Width, board.Height, x, y, cards);
                    if (cube.HasValue)
                    {
                        board.SetCubeAt(new GridPos(x, y), cube.Value);
                    }
                }
            }
            boardView.Rebuild(board, MainBoardWorldSize, MainBoardCenter);
            yield return new WaitForSeconds(AnimBossBeat);
            for (int turn = 0; turn < 2; turn++)
            {
                var motions = new List<LiftMotion>();
                var moves = new List<CellMove>();
                List<GridPos> lifted = AnimBossTurn(board, scene, turn, motions, moves);
                boardView.Refresh();
                // The seams the game hands a moving board's report to, with what the real board
                // code wrote: the survivors ride, the rest are torn off.
                PlayBoardMoves(moves);
                PlayForcedExit(lifted, motions);
                yield return new WaitForSeconds(AnimBossTurnGap);
            }
            animBossLift = null;
            AnimResync();
        }

        /// <summary>The top row of the holed arena: every cell but two, which are holes.</summary>
        private static List<GridPos> AnimHoledTopRow(int w, int h)
        {
            var cells = new List<GridPos>();
            for (int x = 0; x < w; x++)
            {
                if (x != 2 && x != w - 3)
                {
                    cells.Add(new GridPos(x, h - 1));
                }
            }
            return cells;
        }

        /// <summary>The eight steps the direction entry walks, one per press: the four straight
        /// ones, then the diagonals - every way the centrifuge can throw a cube off.</summary>
        private static readonly GridPos[] AnimPeelSteps =
        {
            new GridPos(0, 1), new GridPos(1, 0), new GridPos(0, -1), new GridPos(-1, 0),
            new GridPos(1, 1), new GridPos(1, -1), new GridPos(-1, -1), new GridPos(-1, 1)
        };

        private static readonly string[] AnimPeelStepEnglish =
        {
            "up", "right", "down", "left", "up-right", "down-right", "down-left", "up-left"
        };

        private static readonly string[] AnimPeelStepTurkish =
        {
            "yukarı", "sağ", "aşağı", "sol", "sağ üst", "sağ alt", "sol alt", "sol üst"
        };

        /// <summary>Which of AnimPeelSteps the direction entry plays next.</summary>
        private int animPeelStep;

        private void AnimPeelDirection()
        {
            int index = animPeelStep;
            animPeelStep = (animPeelStep + 1) % AnimPeelSteps.Length;
            StopAnimBossLift();
            animBossLift = StartCoroutine(PeelTestRoutine(AnimPeelSteps[index], LiftReason.ExitedBoard,
                Loc.Pick("thrown off: " + AnimPeelStepEnglish[index], "atılma yönü: " + AnimPeelStepTurkish[index])));
        }

        private void AnimPeelBlocked()
        {
            StopAnimBossLift();
            animBossLift = StartCoroutine(PeelTestRoutine(new GridPos(1, 0), LiftReason.Blocked,
                Loc.Pick("blocked, pushed right", "engelli hedef, sağa itilen")));
        }

        /// <summary>
        /// One step of a moving board, on its own: a lab board with cubes where that step takes
        /// them off - the edge or corner it points at, as many as the cells knob asks and the
        /// edge holds - then the same board without them, and the peel along the step.
        ///
        /// EXITED is what the centrifuge does at a rim. BLOCKED is STAGED: on a board without
        /// holes the rules never block a flung cube (every target is further out and was cleared
        /// first), so it is shown as a column of cubes pushed right against a wall of obsidian.
        /// The motions are the lab's own here - the boss scenes are where Core writes them.
        /// </summary>
        private IEnumerator PeelTestRoutine(GridPos step, LiftReason reason, string label)
        {
            RoundEngine round = session != null ? session.CurrentRound : null;
            if (round == null || round.Board == null || boardView == null)
            {
                animBossLift = null;
                yield break;
            }
            int w = Mathf.Max(6, round.Board.Width);
            int h = Mathf.Max(6, round.Board.Height);
            List<int> cards = AnimBossCards();
            List<GridPos> going = reason == LiftReason.Blocked
                ? AnimBlockedCells(h, AnimCellCount(), w - 4)
                : AnimExitCells(w, h, step, AnimCellCount());
            GameBoard before = AnimPeelBoard(w, h, cards, going, true, reason);
            GameBoard after = AnimPeelBoard(w, h, cards, going, false, reason);
            boardView.Rebuild(before, MainBoardWorldSize, MainBoardCenter);
            animLastLabel = label;
            if (AnimLabOpen)
            {
                RedrawAnimationLab();
            }
            yield return new WaitForSeconds(0.6f);
            var motions = new List<LiftMotion>();
            for (int i = 0; i < going.Count; i++)
            {
                Cube? cube = before.GetCube(going[i]);
                motions.Add(new LiftMotion(going[i], step, reason,
                    cube.HasValue ? cube.Value : new Cube(CubeKind.Normal, 101), false));
            }
            boardView.Rebuild(after, MainBoardWorldSize, MainBoardCenter);
            PlayForcedExit(going, motions);
            yield return new WaitForSeconds(1.2f);
            animBossLift = null;
            AnimResync();
        }

        /// <summary>The cells a step takes off the board, nearest the point it aims at first -
        /// the middle of that edge, or that corner - at most <paramref name="count"/>.</summary>
        private static List<GridPos> AnimExitCells(int w, int h, GridPos step, int count)
        {
            var exits = new List<GridPos>();
            for (int x = 0; x < w; x++)
            {
                for (int y = 0; y < h; y++)
                {
                    int tx = x + step.X;
                    int ty = y + step.Y;
                    if (tx < 0 || tx >= w || ty < 0 || ty >= h)
                    {
                        exits.Add(new GridPos(x, y));
                    }
                }
            }
            float ax = (w - 1) * 0.5f + step.X * w * 0.5f;
            float ay = (h - 1) * 0.5f + step.Y * h * 0.5f;
            exits.Sort(delegate(GridPos a, GridPos b)
            {
                float da = (a.X - ax) * (a.X - ax) + (a.Y - ay) * (a.Y - ay);
                float db = (b.X - ax) * (b.X - ax) + (b.Y - ay) * (b.Y - ay);
                return da.CompareTo(db);
            });
            if (exits.Count > count)
            {
                exits.RemoveRange(count, exits.Count - count);
            }
            return exits;
        }

        /// <summary>A column of cubes to push right into a wall: up to <paramref name="count"/>,
        /// centred on the board's height.</summary>
        private static List<GridPos> AnimBlockedCells(int h, int count, int column)
        {
            var cells = new List<GridPos>();
            int n = Mathf.Clamp(count, 1, h - 2);
            int first = (h - n) / 2;
            for (int i = 0; i < n; i++)
            {
                cells.Add(new GridPos(column, first + i));
            }
            return cells;
        }

        /// <summary>The lab board for PeelTestRoutine: sparse blocks of the run's own, the cubes
        /// that go (or not, for the board after), and for BLOCKED the obsidian wall they meet.</summary>
        private GameBoard AnimPeelBoard(int w, int h, List<int> cards, List<GridPos> going,
            bool withGoing, LiftReason reason)
        {
            var board = new GameBoard(w, h);
            var set = new HashSet<GridPos>(going);
            for (int x = 0; x < w; x++)
            {
                for (int y = 0; y < h; y++)
                {
                    var pos = new GridPos(x, y);
                    if (set.Contains(pos))
                    {
                        if (withGoing)
                        {
                            board.SetCubeAt(pos, AnimCardCube(x, y, cards));
                        }
                        continue;
                    }
                    if (reason == LiftReason.Blocked && set.Contains(new GridPos(x - 1, y)))
                    {
                        board.SetCubeAt(pos, new Cube(CubeKind.Obsidian, -4243));
                        continue;
                    }
                    if (AnimHash(x, y) % 100u < 30u)
                    {
                        board.SetCubeAt(pos, AnimCardCube(x, y, cards));
                    }
                }
            }
            return board;
        }

        /// <summary>Which board each inner-movement entry plays next (0, 1, 2).</summary>
        private int animMoveEscalatorStage;

        private int animMoveCentrifugeStage;

        /// <summary>
        /// A moving board's turn end on a lab board, for its SURVIVORS: the real ShiftRowsUp or
        /// FlingCubesOutward, and what it writes handed to the same two seams the game uses - every
        /// cube that stays rides to its cell, every one that does not is torn off. Each press the
        /// next board: the escalator sparse, dense and nearly full; the centrifuge on an ODD board, so
        /// its centre stands still, first with a full ring round the middle (straight and diagonal
        /// pushes) and a few further out, then mixed, then dense. Two turns each.
        /// </summary>
        private void AnimBoardMoveTest(bool escalator)
        {
            int stage;
            if (escalator)
            {
                stage = animMoveEscalatorStage;
                animMoveEscalatorStage = (stage + 1) % 3;
            }
            else
            {
                stage = animMoveCentrifugeStage;
                animMoveCentrifugeStage = (stage + 1) % 3;
            }
            StopAnimBossLift();
            animBossLift = StartCoroutine(BoardMoveTestRoutine(escalator, stage));
        }

        private IEnumerator BoardMoveTestRoutine(bool escalator, int stage)
        {
            RoundEngine round = session != null ? session.CurrentRound : null;
            if (round == null || round.Board == null || boardView == null)
            {
                animBossLift = null;
                yield break;
            }
            int w = escalator ? Mathf.Max(6, round.Board.Width) : 7;
            int h = escalator ? Mathf.Max(6, round.Board.Height) : 7;
            var board = new GameBoard(w, h);
            List<int> cards = AnimBossCards();
            for (int x = 0; x < w; x++)
            {
                for (int y = 0; y < h; y++)
                {
                    if (AnimMoveCell(escalator, stage, w, h, x, y))
                    {
                        board.SetCubeAt(new GridPos(x, y), AnimCardCube(x, y, cards));
                    }
                }
            }
            boardView.Rebuild(board, MainBoardWorldSize, MainBoardCenter);
            string[] english = escalator
                ? new[] { "sparse", "dense", "nearly full" }
                : new[] { "round the centre", "mixed", "dense" };
            string[] turkish = escalator
                ? new[] { "seyrek", "yoğun", "neredeyse dolu" }
                : new[] { "merkez çevresi", "karışık", "yoğun" };
            animLastLabel = Loc.Pick("inner movement: " + english[stage], "iç hareket: " + turkish[stage]);
            if (AnimLabOpen)
            {
                RedrawAnimationLab();
            }
            yield return new WaitForSeconds(0.7f);
            for (int turn = 0; turn < 2; turn++)
            {
                var motions = new List<LiftMotion>();
                var moves = new List<CellMove>();
                List<GridPos> lifted = escalator
                    ? board.ShiftRowsUp(motions, moves)
                    : board.FlingCubesOutward(motions, moves);
                boardView.Refresh();
                PlayBoardMoves(moves);
                PlayForcedExit(lifted, motions);
                yield return new WaitForSeconds(0.9f);
            }
            animBossLift = null;
            AnimResync();
        }

        /// <summary>Whether a cell of the inner-movement board starts with a cube.</summary>
        private static bool AnimMoveCell(bool escalator, int stage, int w, int h, int x, int y)
        {
            uint roll = AnimHash(x * 3 + 7, y * 5 + 11) % 100u;
            if (escalator)
            {
                return roll < (stage == 0 ? 25u : stage == 1 ? 55u : 88u);
            }
            int ring = Mathf.Max(Mathf.Abs(x - w / 2), Mathf.Abs(y - h / 2));
            if (stage == 0)
            {
                // The centre, which stays; all eight round it; a few further out, and on the rim.
                return ring <= 1 || (ring == 2 && roll < 25u) || (ring == 3 && roll < 30u);
            }
            return roll < (stage == 1 ? 45u : 80u);
        }

        /// <summary>Flips one of the inner movement's debug switches and says which way it went.</summary>
        private void AnimMoveToggle(ref bool flag, string english, string turkish)
        {
            flag = !flag;
            animLastLabel = Loc.Pick("inner movement " + english + ": ", "iç hareket " + turkish + ": ")
                + OnOff(flag);
            if (AnimLabOpen)
            {
                RedrawAnimationLab();
            }
        }

        /// <summary>Flips one of the peel's debug switches and says which way it went.</summary>
        private void AnimPeelToggle(ref bool flag, string english, string turkish)
        {
            flag = !flag;
            animLastLabel = Loc.Pick("momentum peel " + english + ": ", "sökülme " + turkish + ": ")
                + OnOff(flag);
            if (AnimLabOpen)
            {
                RedrawAnimationLab();
            }
        }

        /// <summary>The run's own blocks, for the lab board to be built of - so the scene looks like
        /// the player's board rather than a test pattern. Sorted, so the same run always builds the
        /// same board. Made-up ids (a colour each) when there are none.</summary>
        private List<int> AnimBossCards()
        {
            var ids = new List<int>(cardFaces.Keys);
            ids.Sort();
            if (ids.Count == 0)
            {
                ids.AddRange(new[] { 101, 102, 103, 104, 105 });
            }
            return ids;
        }

        /// <summary>What one cell of the lab board holds: about half full in block-sized clumps of
        /// one card each, and each scene makes sure of the cells its boss is about to act on.</summary>
        private Cube? AnimBossCell(AnimBossScene scene, int w, int h, int x, int y, List<int> cards)
        {
            uint roll = AnimHash(x, y) % 100u;
            bool filled;
            switch (scene)
            {
                case AnimBossScene.Escalator:
                {
                    // A bar along the top and an L under its right end: the two turns' cargo.
                    // The rest of the top two rows is kept sparse so that cargo reads.
                    bool cargo = (y == h - 1 && ((x >= 1 && x <= 3) || x == w - 2))
                        || (y == h - 2 && x >= w - 4 && x <= w - 2);
                    filled = cargo || roll < (y >= h - 2 ? 15u : 46u);
                    break;
                }
                case AnimBossScene.Holes:
                {
                    // Under each of the two top-row holes a cube for each turn to ride into it, and
                    // a few on the top row itself to go over the edge.
                    bool rider = (y == h - 2 || y == h - 3) && (x == 2 || x == w - 3);
                    bool top = y == h - 1 && (x == 0 || x == 1 || x == w - 1);
                    filled = rider || top || roll < (y >= h - 3 ? 12u : 40u);
                    break;
                }
                default:
                {
                    // The centrifuge: a few cubes on the rim for the first fling and more on the
                    // ring inside it for the second. Sparse on the rim, as in the round: every
                    // turn empties it.
                    bool rim = x == 0 || y == 0 || x == w - 1 || y == h - 1;
                    bool ring = !rim && (x == 1 || y == 1 || x == w - 2 || y == h - 2);
                    filled = roll < (rim ? 30u : ring ? 55u : 35u);
                    break;
                }
            }
            if (!filled)
            {
                return null;
            }
            return AnimCardCube(x, y, cards);
        }

        /// <summary>A cube of one of the run's blocks, in block-sized clumps of one card each.</summary>
        private Cube AnimCardCube(int x, int y, List<int> cards)
        {
            int id = cards[(int)(AnimHash(x / 2 + 17, y / 2 + 31) % (uint)cards.Count)];
            BlockCard card = FindOwnedCard(id);
            return new Cube(card != null ? CubeRules.KindForCard(card) : CubeKind.Normal, id);
        }

        /// <summary>One turn end of the scene's boss on the lab board. Returns the cells it reports
        /// as lifted - exactly what the game hands PlayForcedExit.</summary>
        private static List<GridPos> AnimBossTurn(GameBoard board, AnimBossScene scene, int turn,
            List<LiftMotion> motions, List<CellMove> moves)
        {
            switch (scene)
            {
                case AnimBossScene.Escalator:
                case AnimBossScene.Holes:
                    return board.ShiftRowsUp(motions, moves);
                default:
                    return board.FlingCubesOutward(motions, moves);
            }
        }

        // ------------------------------------------------------------------ kangren

        /// <summary>The nine things the rot does, one entry each.</summary>
        private enum AnimRotScene
        {
            /// <summary>It takes an empty cell: the floor is contaminated, the dead mass rises.</summary>
            Empty,

            /// <summary>It converts the cube standing there, from the side it came in on.</summary>
            Occupied,

            /// <summary>And what it pressed against and could not take.</summary>
            Immune,

            /// <summary>Nothing happens: this is what standing rot and a dead line look like.</summary>
            Presence,

            /// <summary>The turn it bills the player, felt through every rotten cube at once.</summary>
            Billed,

            /// <summary>A row taken whole - the bottom one, so there is no jump to distract.</summary>
            DeadRow,

            /// <summary>The same for a column.</summary>
            DeadColumn,

            /// <summary>A row dies and the rot jumps to the edge row, turning what stands there.</summary>
            EdgeJump,

            /// <summary>A cascade: row, jump, column, jump, row - three steps.</summary>
            Chain
        }

        /// <summary>The rot scene playing, so pressing it again restarts it.</summary>
        private Coroutine animRot;

        /// <summary>Which side the spread scenes bring the rot in from, one on per press.</summary>
        private int animRotSide;

        /// <summary>The cell a staged scene finishes its line at, and the rotten cell feeding it -
        /// the only things the lab decides. Everything that follows is the rules'.</summary>
        private GridPos animRotTarget;

        private GridPos animRotSource;

        /// <summary>Right, up, left, down - the four sides the rot can come in from.</summary>
        private static readonly GridPos[] AnimRotSides =
        {
            new GridPos(1, 0), new GridPos(0, 1), new GridPos(-1, 0), new GridPos(0, -1)
        };

        /// <summary>A patch that has been standing a while, around the middle of the board.</summary>
        private static readonly GridPos[] AnimRotPatch =
        {
            new GridPos(0, 0), new GridPos(1, 0), new GridPos(-1, 0), new GridPos(0, 1),
            new GridPos(1, 1), new GridPos(0, -1), new GridPos(-1, 1), new GridPos(2, 0)
        };

        /// <summary>Card id for the walls the spread scenes are fenced with: negative, so no card is
        /// ever found for them and they wear obsidian's and gold's own faces.</summary>
        private const int AnimRotWallCard = -11;

        /// <summary>How long a rot scene is left standing before the round's board comes back.</summary>
        private const float AnimRotWatch = 3.2f;

        /// <summary>A cascade is three steps long, so it needs longer.</summary>
        private const float AnimRotChainWatch = 6.5f;

        /// <summary>
        /// One thing the rot does, on a board of the lab's OWN and through the same seam the game
        /// plays it through (PlayGangreneScene). What the lab fabricates is the ARGUMENTS: the shape
        /// of the board, and for the staged scenes which cell finishes the line. The spread itself is
        /// the real GameBoard.SpreadGangrene (the board is built so it has exactly one cell it can
        /// take, which is what makes a press repeatable), and every death, jump and cascade is the
        /// real GameBoard.InfectFullLines - so the dead lines are really dead and their bands and
        /// washes are the rules', not a drawing of them.
        ///
        /// The round's own board is never touched; AnimResync puts it back at the end.
        /// </summary>
        private void AnimRot(AnimRotScene scene)
        {
            StopAnimRot();
            if (scene == AnimRotScene.Empty || scene == AnimRotScene.Occupied
                || scene == AnimRotScene.Immune)
            {
                animRotSide = (animRotSide + 1) & 3;
                animLastLabel = Loc.Pick("rot from the ", "kangren şu yönden: ")
                    + AnimRotSideName(animRotSide);
            }
            animRot = StartCoroutine(RotRoutine(scene));
        }

        private void StopAnimRot()
        {
            if (animRot != null)
            {
                StopCoroutine(animRot);
                animRot = null;
            }
            boardView.StopGangrene();
        }

        private static string AnimRotSideName(int side)
        {
            switch (side & 3)
            {
                case 0: return Loc.Pick("right", "sağdan");
                case 1: return Loc.Pick("above", "yukarıdan");
                case 2: return Loc.Pick("left", "soldan");
                default: return Loc.Pick("below", "aşağıdan");
            }
        }

        private IEnumerator RotRoutine(AnimRotScene scene)
        {
            RoundEngine round = session != null ? session.CurrentRound : null;
            if (round == null || round.Board == null || boardView == null)
            {
                animRot = null;
                yield break;
            }
            // Never smaller than 7 x 7: the cascade needs a row with an edge row under it, a column
            // and an edge column beside it.
            int w = Mathf.Max(7, round.Board.Width);
            int h = Mathf.Max(7, round.Board.Height);
            var board = new GameBoard(w, h);
            List<int> cards = AnimBossCards();
            AnimRotBoard(board, scene, cards);
            boardView.Rebuild(board, MainBoardWorldSize, MainBoardCenter);
            yield return new WaitForSeconds(AnimBossBeat);
            var turn = new GangreneView.TurnScene { Delay = 0.06f };
            AnimRotTurn(board, scene, turn);
            boardView.Refresh();
            PlayGangreneScene(turn);
            yield return new WaitForSeconds(scene == AnimRotScene.Chain
                ? AnimRotChainWatch : AnimRotWatch);
            animRot = null;
            AnimResync();
        }

        /// <summary>Lays the lab board out for one scene, and marks the cell a staged one finishes
        /// its line at. Nothing here kills a line: that is AnimRotTurn's, through the rules.</summary>
        private void AnimRotBoard(GameBoard board, AnimRotScene scene, List<int> cards)
        {
            int w = board.Width;
            int h = board.Height;
            var rot = new Cube(CubeKind.Gangrene, GameBoard.GangreneCardId);
            var reserved = new HashSet<GridPos>();
            animRotTarget = new GridPos(w / 2, h / 2);
            animRotSource = animRotTarget;
            switch (scene)
            {
                case AnimRotScene.Empty:
                case AnimRotScene.Occupied:
                case AnimRotScene.Immune:
                {
                    // ONE cell it can take, so the real spread has nothing to choose and a press
                    // always shows the same thing: everything else round the patch is obsidian,
                    // which nothing can infect.
                    GridPos step = AnimRotSides[animRotSide & 3];
                    var source = new GridPos(animRotTarget.X + step.X, animRotTarget.Y + step.Y);
                    animRotSource = source;
                    reserved.Add(animRotTarget);
                    reserved.Add(source);
                    board.SetCubeAt(source, rot);
                    for (int i = 0; i < AnimRotSides.Length; i++)
                    {
                        var n = new GridPos(source.X + AnimRotSides[i].X, source.Y + AnimRotSides[i].Y);
                        if (!board.IsInside(n) || (n.X == animRotTarget.X && n.Y == animRotTarget.Y))
                        {
                            continue;
                        }
                        board.SetCubeAt(n, new Cube(CubeKind.Obsidian, AnimRotWallCard));
                        reserved.Add(n);
                    }
                    if (scene != AnimRotScene.Empty)
                    {
                        board.SetCubeAt(animRotTarget,
                            AnimCardCube(animRotTarget.X, animRotTarget.Y, cards));
                    }
                    if (scene == AnimRotScene.Immune)
                    {
                        // And what it will be pressing against once it is in: gold and obsidian,
                        // the two things it can never take.
                        for (int i = 0; i < AnimRotSides.Length; i++)
                        {
                            var n = new GridPos(animRotTarget.X + AnimRotSides[i].X,
                                animRotTarget.Y + AnimRotSides[i].Y);
                            if (!board.IsInside(n) || (n.X == source.X && n.Y == source.Y))
                            {
                                continue;
                            }
                            board.SetCubeAt(n, new Cube(
                                i % 2 == 0 ? CubeKind.Gold : CubeKind.Obsidian, AnimRotWallCard));
                            reserved.Add(n);
                        }
                    }
                    break;
                }
                case AnimRotScene.Presence:
                case AnimRotScene.Billed:
                {
                    // A patch that has been standing a while, and a row the rot already took whole.
                    // AnimRotTurn kills that line through the RULES before the board goes up, so
                    // its band and its wash are real and there is nothing to watch - the point.
                    for (int x = 0; x < w; x++)
                    {
                        var cell = new GridPos(x, 2);
                        board.SetCubeAt(cell, rot);
                        reserved.Add(cell);
                    }
                    for (int i = 0; i < AnimRotPatch.Length; i++)
                    {
                        var cell = new GridPos(w / 2 + AnimRotPatch[i].X, h / 2 + AnimRotPatch[i].Y);
                        if (!board.IsInside(cell))
                        {
                            continue;
                        }
                        board.SetCubeAt(cell, rot);
                        reserved.Add(cell);
                    }
                    break;
                }
                case AnimRotScene.DeadRow:
                {
                    // The BOTTOM row: the nearer edge of a row that low is itself, so the rot has
                    // nowhere to jump and this entry is the death and nothing else.
                    animRotTarget = new GridPos(w / 2, 0);
                    animRotSource = new GridPos(w / 2 - 1, 0);
                    for (int x = 0; x < w; x++)
                    {
                        var cell = new GridPos(x, 0);
                        reserved.Add(cell);
                        board.SetCubeAt(cell, x == animRotTarget.X
                            ? AnimCardCube(x, 0, cards) : rot);
                    }
                    break;
                }
                case AnimRotScene.DeadColumn:
                {
                    animRotTarget = new GridPos(0, h / 2);
                    animRotSource = new GridPos(0, h / 2 - 1);
                    for (int y = 0; y < h; y++)
                    {
                        var cell = new GridPos(0, y);
                        reserved.Add(cell);
                        board.SetCubeAt(cell, y == animRotTarget.Y
                            ? AnimCardCube(0, y, cards) : rot);
                    }
                    break;
                }
                case AnimRotScene.EdgeJump:
                {
                    // Two rows up from the bottom: it dies, and the rot jumps to the bottom row,
                    // turning the cubes standing there. Two gaps in that row, so it is not taken
                    // whole itself - and so the empty cells can be seen staying empty.
                    animRotTarget = new GridPos(w / 2, 2);
                    animRotSource = new GridPos(w / 2 - 1, 2);
                    for (int x = 0; x < w; x++)
                    {
                        var line = new GridPos(x, 2);
                        reserved.Add(line);
                        board.SetCubeAt(line, x == animRotTarget.X
                            ? AnimCardCube(x, 2, cards) : rot);
                        var edge = new GridPos(x, 0);
                        reserved.Add(edge);
                        if (x != 1 && x != w - 2)
                        {
                            board.SetCubeAt(edge, AnimCardCube(x, 0, cards));
                        }
                        // The row between them is left clear, so the pressure paths read.
                        reserved.Add(new GridPos(x, 1));
                    }
                    break;
                }
                default:
                {
                    // THE CASCADE. Row 3 dies and its jump turns the whole bottom row; that
                    // completes the column two in from the right, whose own jump turns the cubes on
                    // the right edge; and the bottom row - all rot by then - dies on the next pass.
                    int column = w - 2;
                    animRotTarget = new GridPos(0, 3);
                    animRotSource = new GridPos(1, 3);
                    for (int x = 0; x < w; x++)
                    {
                        var line = new GridPos(x, 3);
                        reserved.Add(line);
                        board.SetCubeAt(line, x == animRotTarget.X
                            ? AnimCardCube(x, 3, cards) : rot);
                        var bottom = new GridPos(x, 0);
                        reserved.Add(bottom);
                        board.SetCubeAt(bottom, AnimCardCube(x, 0, cards));
                    }
                    for (int y = 1; y < h; y++)
                    {
                        var cell = new GridPos(column, y);
                        reserved.Add(cell);
                        if (y != 3)
                        {
                            board.SetCubeAt(cell, rot);
                        }
                    }
                    for (int y = 1; y < h; y++)
                    {
                        // Three cubes for the column's jump to turn, and the rest of the edge column
                        // left empty so IT does not die as well.
                        var cell = new GridPos(w - 1, y);
                        reserved.Add(cell);
                        if (y == 1 || y == 2 || y == 4)
                        {
                            board.SetCubeAt(cell, AnimCardCube(w - 1, y, cards));
                        }
                    }
                    break;
                }
            }
            AnimRotFill(board, cards, scene == AnimRotScene.Chain ? 22u : 34u, reserved);
        }

        /// <summary>The rest of the lab board in the run's own blocks, in block-sized clumps, so the
        /// scene looks like a real arena. Never a reserved cell: those belong to whatever the scene
        /// is about. <paramref name="reserved"/> may be null - a scene that has nothing to protect
        /// (Mapus picks its own cell AFTER the board is filled) should not have to hand over an
        /// empty set to say so.</summary>
        private void AnimRotFill(GameBoard board, List<int> cards, uint density,
            HashSet<GridPos> reserved)
        {
            for (int x = 0; x < board.Width; x++)
            {
                for (int y = 0; y < board.Height; y++)
                {
                    var cell = new GridPos(x, y);
                    if ((reserved != null && reserved.Contains(cell))
                        || AnimHash(x, y) % 100u >= density)
                    {
                        continue;
                    }
                    board.SetCubeAt(cell, AnimCardCube(x, y, cards));
                }
            }
        }

        /// <summary>Runs the REAL rot rules on the lab board and fills in the scene from what they
        /// reported - the same thing RoundEngine hands the game's own PlayGangrene.</summary>
        private void AnimRotTurn(GameBoard board, AnimRotScene scene, GangreneView.TurnScene turn)
        {
            var deaths = new List<GangreneLineDeath>();
            if (scene == AnimRotScene.Presence || scene == AnimRotScene.Billed)
            {
                // The line dies before anyone is looking: this is the standing state, not an event.
                board.InfectFullLines(null);
                turn.Billed = scene == AnimRotScene.Billed;
                return;
            }
            if (scene == AnimRotScene.Empty || scene == AnimRotScene.Occupied
                || scene == AnimRotScene.Immune)
            {
                GangreneSpread spread;
                board.SpreadGangrene(new SeededRandom(4041 + animRotSide), out spread);
                if (spread != null)
                {
                    turn.Cell = spread.Cell;
                    turn.Source = spread.Source;
                    turn.HadCube = spread.Before.HasValue;
                    if (spread.Before.HasValue)
                    {
                        turn.Before = LookOf(spread.Before.Value);
                    }
                    for (int i = 0; i < spread.Immune.Count; i++)
                    {
                        turn.Immune.Add(spread.Immune[i]);
                    }
                }
            }
            else
            {
                // The staged scenes: the lab puts the last cell of the line in itself, since a board
                // with a row of rot on it has far too many cells the spread could take.
                Cube? before = board.GetCube(animRotTarget);
                turn.Cell = animRotTarget;
                turn.Source = animRotSource;
                turn.HadCube = before.HasValue;
                if (before.HasValue)
                {
                    turn.Before = LookOf(before.Value);
                    board.SetCubeKind(animRotTarget, CubeKind.Gangrene);
                }
                else
                {
                    board.SetCubeAt(animRotTarget, new Cube(CubeKind.Gangrene, GameBoard.GangreneCardId));
                }
            }
            board.InfectFullLines(deaths);
            for (int d = 0; d < deaths.Count; d++)
            {
                GangreneLineDeath death = deaths[d];
                var line = new GangreneView.LineDeath
                {
                    IsRow = death.IsRow,
                    Line = death.Line,
                    EdgeLine = death.EdgeLine
                };
                for (int i = 0; i < death.Converted.Count; i++)
                {
                    line.Converted.Add(new GangreneView.Converted
                    {
                        Cell = death.Converted[i],
                        Before = i < death.Before.Count
                            ? LookOf(death.Before[i])
                            : default(ClusterBurstView.Look)
                    });
                }
                turn.Deaths.Add(line);
            }
        }



        // ------------------------------------------------------------------ yılan

        /// <summary>Every snake scenario the lab can put up.</summary>
        private enum AnimSnakeScene
        {
            Spawn8,
            Spawn12,
            Spawn20,
            Idle,
            Slide1,
            Slide3,
            SlideLong,
            SlideTurn,
            Stuck,
            EatNormal,
            EatColour,
            EatWater,
            EatFire,
            EatObsidian,
            EatGold,
            EatAndGrow,
            Cut1,
            Cut2,
            Cut3,
            Defeat,
            FullTurn,
            LineClear,
            Directions,
            Shapes,
            BiteDirections
        }

        private Coroutine animSnake;

        /// <summary>Which way the direction and bite tests go next, one per press.</summary>
        private int animSnakeDirection;

        /// <summary>Which body shape the shape test shows next.</summary>
        private int animSnakeShape;

        /// <summary>Which plain block the bite row is feeding it - one press, one colour. Static
        /// because the food is chosen in AnimSnakeFood, which has no instance to ask.</summary>
        private static int animEatColour;

        /// <summary>Right, up, left, down - the four ways a snake can go.</summary>
        private static readonly GridPos[] AnimSnakeSteps =
        {
            new GridPos(1, 0), new GridPos(0, 1), new GridPos(-1, 0), new GridPos(0, -1)
        };

        private static readonly string[] AnimSnakeStepNames = { "sağa", "yukarı", "sola", "aşağı" };

        /// <summary>
        /// One snake scenario, on a board of the lab's own and through the same seams the game uses:
        /// PlaySnakeBody puts it there, PlaySnakeScene plays what it did. The lab fabricates the
        /// ARGUMENTS only - the shape, the food, the number of lines that crossed it - exactly as
        /// Core would have reported them, so what plays is the real animation.
        /// </summary>
        private void AnimSnake(AnimSnakeScene scene)
        {
            StopAnimSnake();
            StopAnimBossLift();
            StopAnimRot();
            StopAnimRaw();
            if (scene == AnimSnakeScene.Directions || scene == AnimSnakeScene.BiteDirections)
            {
                animSnakeDirection = (animSnakeDirection + 1) & 3;
                animLastLabel = Loc.Pick("snake: ", "yılan: ") + AnimSnakeStepNames[animSnakeDirection];
            }
            if (scene == AnimSnakeScene.Shapes)
            {
                animSnakeShape = (animSnakeShape + 1) % 5;
            }
            animSnake = StartCoroutine(SnakeRoutine(scene));
        }

        private void StopAnimSnake()
        {
            if (animSnake != null)
            {
                StopCoroutine(animSnake);
                animSnake = null;
            }
            boardView.StopSnake();
        }

        private IEnumerator SnakeRoutine(AnimSnakeScene scene)
        {
            RoundEngine round = session != null ? session.CurrentRound : null;
            if (round == null || round.Board == null || boardView == null)
            {
                animSnake = null;
                yield break;
            }
            // Nine wide, so a long slide has somewhere to go and a 20-segment snake still fits.
            int w = Mathf.Max(9, round.Board.Width);
            int h = Mathf.Max(9, round.Board.Height);
            var board = new GameBoard(w, h);
            List<int> cards = AnimBossCards();
            var body = new List<GridPos>();
            GridPos step = AnimSnakeSteps[animSnakeDirection];
            AnimSnakeSetup(board, scene, cards, body, ref step);
            boardView.Rebuild(board, MainBoardWorldSize, MainBoardCenter);
            bool waking = scene == AnimSnakeScene.Spawn8 || scene == AnimSnakeScene.Spawn12
                || scene == AnimSnakeScene.Spawn20;
            PlaySnakeBody(body, waking);
            yield return new WaitForSeconds(waking ? 0.1f : AnimBossBeat);
            SnakeView.TurnScene turn = AnimSnakeTurn(board, scene, body, step);
            if (turn != null)
            {
                if (scene == AnimSnakeScene.LineClear)
                {
                    // The player's own line goes off FIRST: that is the cause the cut answers.
                    FlashLine(board, turn.Cuts[0].Trigger.Y, true);
                    yield return new WaitForSeconds(0.12f);
                }
                AnimSnakeApply(board, turn);
                boardView.Refresh();
                PlaySnakeScene(turn);
            }
            float guard = 0f;
            while (boardView.Snake.Busy && guard < 8f)
            {
                guard += Time.deltaTime;
                yield return null;
            }
            // AND IT STAYS. The state worth looking at in a snake scene is the one the turn
            // ENDS in - a segment longer, the head in the cell it emptied - so the lab holds it
            // like every other entry does, until RESET or closing the lab puts the round back.
            // It used to resync itself a breath later, which read as the snake biting and then
            // going straight back to how it was.
            if (turn != null)
            {
                AnimSnakeSayResult(turn);
            }
            animSnake = null;
        }

        /// <summary>Lays the snake and whatever is standing in front of it, and says which way the
        /// scene's move goes.</summary>
        private void AnimSnakeSetup(GameBoard board, AnimSnakeScene scene, List<int> cards,
            List<GridPos> body, ref GridPos step)
        {
            int w = board.Width;
            int h = board.Height;
            int row = h / 2;
            switch (scene)
            {
                case AnimSnakeScene.Spawn8:
                    AnimSnakeCoil(board, body, 8);
                    break;
                case AnimSnakeScene.Spawn12:
                    AnimSnakeCoil(board, body, 12);
                    break;
                case AnimSnakeScene.Spawn20:
                    AnimSnakeCoil(board, body, 20);
                    break;
                case AnimSnakeScene.Idle:
                    AnimSnakeShape(board, body, 3, 1);
                    break;
                case AnimSnakeScene.Shapes:
                    AnimSnakeShape(board, body, 2, animSnakeShape);
                    break;
                case AnimSnakeScene.Slide1:
                    // Two cells from the wall: it slides one and the wall stops it.
                    AnimSnakeRun(board, body, new GridPos(w - 2, row), new GridPos(-1, 0), 7);
                    step = new GridPos(1, 0);
                    break;
                case AnimSnakeScene.Slide3:
                    AnimSnakeRun(board, body, new GridPos(w - 4, row), new GridPos(-1, 0), 6);
                    step = new GridPos(1, 0);
                    break;
                case AnimSnakeScene.SlideLong:
                    AnimSnakeRun(board, body, new GridPos(2, row), new GridPos(-1, 0), 2);
                    step = new GridPos(1, 0);
                    break;
                case AnimSnakeScene.SlideTurn:
                    // Lying along the row, and the move goes UP: the head comes round and a corner
                    // forms behind it.
                    AnimSnakeRun(board, body, new GridPos(w / 2, 1), new GridPos(-1, 0), 7);
                    step = new GridPos(0, 1);
                    break;
                case AnimSnakeScene.Stuck:
                    // Its own body on one side, the arena's corner on the other two.
                    body.Add(new GridPos(0, 0));
                    body.Add(new GridPos(1, 0));
                    body.Add(new GridPos(1, 1));
                    body.Add(new GridPos(0, 1));
                    body.Add(new GridPos(0, 2));
                    body.Add(new GridPos(1, 2));
                    AnimSnakePlace(board, body);
                    step = new GridPos(1, 0);
                    break;
                case AnimSnakeScene.EatNormal:
                case AnimSnakeScene.EatColour:
                case AnimSnakeScene.EatWater:
                case AnimSnakeScene.EatFire:
                case AnimSnakeScene.EatObsidian:
                case AnimSnakeScene.EatGold:
                {
                    AnimSnakeRun(board, body, new GridPos(w / 2, row), new GridPos(-1, 0), 7);
                    step = new GridPos(1, 0);
                    var food = new GridPos(w / 2 + 1, row);
                    board.SetCubeAt(food, AnimSnakeFood(scene, food, cards));
                    break;
                }
                case AnimSnakeScene.EatAndGrow:
                case AnimSnakeScene.FullTurn:
                {
                    AnimSnakeRun(board, body, new GridPos(w - 6, row), new GridPos(-1, 0), 6);
                    step = new GridPos(1, 0);
                    var food = new GridPos(w - 2, row);
                    board.SetCubeAt(food, AnimSnakeFood(AnimSnakeScene.EatNormal, food, cards));
                    break;
                }
                case AnimSnakeScene.BiteDirections:
                {
                    var head = new GridPos(w / 2, h / 2);
                    GridPos back = new GridPos(-step.X, -step.Y);
                    AnimSnakeRun(board, body, head, back, 6);
                    var food = new GridPos(head.X + step.X, head.Y + step.Y);
                    board.SetCubeAt(food, AnimSnakeFood(AnimSnakeScene.EatNormal, food, cards));
                    break;
                }
                case AnimSnakeScene.Directions:
                {
                    var head = new GridPos(w / 2, h / 2);
                    AnimSnakeRun(board, body, head, new GridPos(-step.X, -step.Y), 6);
                    break;
                }
                case AnimSnakeScene.Defeat:
                    AnimSnakeRun(board, body, new GridPos(w / 2, row), new GridPos(-1, 0), 3);
                    break;
                default:
                    AnimSnakeRun(board, body, new GridPos(w / 2 + 2, row), new GridPos(-1, 0), 8);
                    break;
            }
            // A board with something on it: the snake is not floating in an empty arena.
            var reserved = new HashSet<GridPos>(body);
            for (int x = 0; x < w; x++)
            {
                for (int y = 0; y < h; y++)
                {
                    var cell = new GridPos(x, y);
                    if (!reserved.Contains(cell) && board.GetCube(cell).HasValue)
                    {
                        reserved.Add(cell);
                    }
                }
            }
            // AND ITS PATH STAYS CLEAR. Without this the fill drops blocks in front of the snake,
            // so "slides three cells" slides one and eats instead - the entry stops doing what its
            // name says, and a bug cannot be told apart from the dice. Whatever a scene WANTS in
            // the way it has already put there, and an existing cube is reserved above.
            if (body.Count > 0 && (step.X != 0 || step.Y != 0))
            {
                var ahead = body[0];
                for (int i = 0; i < w + h; i++)
                {
                    ahead = new GridPos(ahead.X + step.X, ahead.Y + step.Y);
                    if (!board.IsInside(ahead))
                    {
                        break;
                    }
                    reserved.Add(ahead);
                }
            }
            AnimRotFill(board, cards, 16u, reserved);
        }

        private static Cube AnimSnakeFood(AnimSnakeScene scene, GridPos cell, List<int> cards)
        {
            switch (scene)
            {
                case AnimSnakeScene.EatObsidian:
                    return new Cube(CubeKind.Obsidian, AnimRotWallCard);
                case AnimSnakeScene.EatGold:
                    return new Cube(CubeKind.Gold, AnimRotWallCard);
                case AnimSnakeScene.EatWater:
                    return new Cube(CubeKind.Water, cards.Count > 0 ? cards[0] : 101);
                case AnimSnakeScene.EatFire:
                    return new Cube(CubeKind.Fire, cards.Count > 0 ? cards[0] : 101);
                case AnimSnakeScene.EatColour:
                    // A DIFFERENT plain block every press. Nothing in the effect knows about
                    // block types, so each of these has to come out its own colour - and if two
                    // of them look the same, the colour is not being taken off the block.
                    return new Cube(CubeKind.Normal, cards.Count > 0
                        ? cards[animEatColour % cards.Count] : 101);
                default:
                    return new Cube(CubeKind.Normal, cards.Count > 0 ? cards[0] : 101);
            }
        }

        /// <summary>A straight snake: the head at <paramref name="head"/> and the body running away
        /// from it along <paramref name="back"/>, clipped to the arena.</summary>
        private static void AnimSnakeRun(GameBoard board, List<GridPos> body, GridPos head,
            GridPos back, int length)
        {
            var at = head;
            for (int i = 0; i < length; i++)
            {
                if (!board.IsInside(at))
                {
                    break;
                }
                body.Add(at);
                at = new GridPos(at.X + back.X, at.Y + back.Y);
            }
            AnimSnakePlace(board, body);
        }

        /// <summary>The serpentine the rules lay a snake in at the start of a round, so the wake-up
        /// is watched on the shape it really has.</summary>
        private static void AnimSnakeCoil(GameBoard board, List<GridPos> body, int length)
        {
            for (int y = 0; y < board.Height && body.Count < length; y++)
            {
                bool leftToRight = (y % 2) == 0;
                for (int i = 0; i < board.Width && body.Count < length; i++)
                {
                    int x = leftToRight ? i : board.Width - 1 - i;
                    body.Add(new GridPos(x, y));
                }
            }
            body.Reverse();
            AnimSnakePlace(board, body);
        }

        /// <summary>The five shapes the topology has to survive: straight, one corner, several, an
        /// S, and a long serpentine.</summary>
        private static void AnimSnakeShape(GameBoard board, List<GridPos> body, int y0, int shape)
        {
            var path = new List<GridPos>();
            var at = new GridPos(1, y0);
            var moves = new List<GridPos>();
            switch (shape)
            {
                case 0:
                    moves.AddRange(new[] { new GridPos(1, 0), new GridPos(1, 0), new GridPos(1, 0),
                        new GridPos(1, 0), new GridPos(1, 0), new GridPos(1, 0) });
                    break;
                case 1:
                    moves.AddRange(new[] { new GridPos(1, 0), new GridPos(1, 0), new GridPos(1, 0),
                        new GridPos(0, 1), new GridPos(0, 1), new GridPos(0, 1) });
                    break;
                case 2:
                    moves.AddRange(new[] { new GridPos(1, 0), new GridPos(0, 1), new GridPos(1, 0),
                        new GridPos(0, 1), new GridPos(1, 0), new GridPos(0, 1), new GridPos(1, 0) });
                    break;
                case 3:
                    moves.AddRange(new[] { new GridPos(1, 0), new GridPos(1, 0), new GridPos(0, 1),
                        new GridPos(0, 1), new GridPos(1, 0), new GridPos(1, 0), new GridPos(0, 1),
                        new GridPos(0, 1), new GridPos(1, 0) });
                    break;
                default:
                    for (int i = 0; i < 4; i++)
                    {
                        moves.Add(new GridPos(1, 0));
                    }
                    moves.Add(new GridPos(0, 1));
                    for (int i = 0; i < 4; i++)
                    {
                        moves.Add(new GridPos(-1, 0));
                    }
                    moves.Add(new GridPos(0, 1));
                    for (int i = 0; i < 4; i++)
                    {
                        moves.Add(new GridPos(1, 0));
                    }
                    break;
            }
            path.Add(at);
            for (int i = 0; i < moves.Count; i++)
            {
                at = new GridPos(at.X + moves[i].X, at.Y + moves[i].Y);
                if (!board.IsInside(at))
                {
                    break;
                }
                path.Add(at);
            }
            path.Reverse();                // head first, as the rules keep it
            body.AddRange(path);
            AnimSnakePlace(board, body);
        }

        private static void AnimSnakePlace(GameBoard board, List<GridPos> body)
        {
            for (int i = 0; i < body.Count; i++)
            {
                if (board.IsInside(body[i]))
                {
                    board.SetCubeAt(body[i], new Cube(CubeKind.Snake, SnakeBoss.SnakeCardId));
                }
            }
        }

        /// <summary>
        /// What the scene's turn WAS: the same shape of report Core writes - the cuts in order with
        /// the tail cell each one took, and the slide cell by cell with what it ate at the end.
        /// The slide is walked by the same rule the boss uses (on it goes until a wall, its own body
        /// or a block stops it, and a block that stops it is eaten).
        /// </summary>
        private SnakeView.TurnScene AnimSnakeTurn(GameBoard board, AnimSnakeScene scene,
            List<GridPos> body, GridPos step)
        {
            if (scene == AnimSnakeScene.Idle || scene == AnimSnakeScene.Shapes
                || scene == AnimSnakeScene.Spawn8 || scene == AnimSnakeScene.Spawn12
                || scene == AnimSnakeScene.Spawn20)
            {
                return null;
            }
            var turn = new SnakeView.TurnScene();
            turn.BodyBefore.AddRange(body);
            var standing = new List<GridPos>(body);
            int cuts = scene == AnimSnakeScene.Cut1 || scene == AnimSnakeScene.LineClear ? 1
                : scene == AnimSnakeScene.Cut2 ? 2
                : scene == AnimSnakeScene.Cut3 ? 3
                : scene == AnimSnakeScene.Defeat ? standing.Count : 0;
            for (int i = 0; i < cuts && standing.Count > 0; i++)
            {
                var after = new List<GridPos>(standing);
                GridPos tail = after[after.Count - 1];
                after.RemoveAt(after.Count - 1);
                // The line that did it crosses the snake where the head is: the signal then has the
                // whole body to travel down, which is the thing worth watching.
                turn.Cuts.Add(new SnakeView.CutStep
                {
                    Trigger = standing[Mathf.Min(i, standing.Count - 1)],
                    RemovedTail = tail,
                    BodyAfter = after
                });
                standing = after;
            }
            if (standing.Count == 0)
            {
                turn.Defeated = true;
                turn.BodyAfter.AddRange(standing);
                return turn;
            }
            if (cuts > 0)
            {
                // A cut turn in the lab is about the cut: nothing slides afterwards.
                turn.BodyAfter.AddRange(standing);
                return turn;
            }
            if (scene == AnimSnakeScene.Stuck)
            {
                turn.Stuck = true;
                turn.BodyAfter.AddRange(standing);
                return turn;
            }
            // The slide, by the boss's own rule.
            var live = new List<GridPos>(standing);
            for (int guard = 0; guard < board.Width + board.Height; guard++)
            {
                var next = new GridPos(live[0].X + step.X, live[0].Y + step.Y);
                if (!board.IsInside(next) || live.Contains(next))
                {
                    break;
                }
                Cube? food = board.GetCube(next);
                bool ate = food.HasValue;
                live.Insert(0, next);
                if (!ate)
                {
                    live.RemoveAt(live.Count - 1);
                }
                turn.Steps.Add(new List<GridPos>(live));
                if (ate)
                {
                    turn.EatenCell = next;
                    turn.EatenLook = LookOf(food.Value);
                    turn.Grew = true;
                    break;
                }
            }
            turn.BodyAfter.AddRange(live);
            return turn;
        }

        /// <summary>Leaves the lab board where the turn leaves it: the snake in its new cells, and
        /// whatever it ate gone - exactly the state Core would have handed the View.</summary>
        private static void AnimSnakeApply(GameBoard board, SnakeView.TurnScene turn)
        {
            for (int i = 0; i < turn.BodyBefore.Count; i++)
            {
                board.DestroyCubeForced(turn.BodyBefore[i]);
            }
            if (turn.EatenCell.HasValue)
            {
                board.DestroyCubeForced(turn.EatenCell.Value);
            }
            for (int i = 0; i < turn.BodyAfter.Count; i++)
            {
                if (board.IsInside(turn.BodyAfter[i]))
                {
                    board.SetCubeAt(turn.BodyAfter[i], new Cube(CubeKind.Snake, SnakeBoss.SnakeCardId));
                }
            }
        }

        /// <summary>What the turn ENDED as, against what actually got drawn. The rules and the
        /// drawing agreeing is the whole contract of this boss, so the lab states it out loud
        /// rather than leaving it to be measured off the screen by eye.</summary>
        private void AnimSnakeSayResult(SnakeView.TurnScene turn)
        {
            List<GridPos> said = turn.BodyAfter;
            IReadOnlyList<GridPos> drew = boardView.Snake.DrawnBody;
            string eaten = turn.EatenCell.HasValue
                ? string.Format("{0},{1}", turn.EatenCell.Value.X, turn.EatenCell.Value.Y) : "-";
            string head = said.Count > 0
                ? string.Format("{0},{1}", said[0].X, said[0].Y) : "-";
            string at = drew != null && drew.Count > 0
                ? string.Format("{0},{1}", drew[0].X, drew[0].Y) : "-";
            bool agree = drew != null && drew.Count == said.Count && head == at;
            animLastLabel = Loc.Pick(
                string.Format("snake: rules say head {0}, {1} segments, ate {2} - drawn head {3}"
                    + ", {4} segments{5}", head, said.Count, eaten, at,
                    drew != null ? drew.Count : 0, agree ? "" : "  <-- DISAGREE"),
                string.Format("yılan: kurallar kafa {0}, {1} segment, yenen {2} - çizilen kafa {3}"
                    + ", {4} segment{5}", head, said.Count, eaten, at,
                    drew != null ? drew.Count : 0, agree ? "" : "  <-- UYUŞMUYOR"));
            if (AnimLabOpen)
            {
                RedrawAnimationLab();
            }
        }

        /// <summary>The boss's end, on a palette handed in by hand - null means "whatever this
        /// round actually ate", an empty array means "it ate nothing", and anything else stands in
        /// for a round that swallowed those colours.</summary>
        private void AnimSnakeDefeat(Color[] palette)
        {
            if (palette != null)
            {
                SnakeDefeatView.OverrideHistory(palette);
            }
            animLastLabel = Loc.Pick(
                string.Format("snake beaten: {0} colours in its palette",
                    palette == null ? SnakeDefeatView.RememberedCount : palette.Length),
                string.Format("yılan yenilgi: paletinde {0} renk",
                    palette == null ? SnakeDefeatView.RememberedCount : palette.Length));
            AnimSnake(AnimSnakeScene.Defeat);
        }

        /// <summary>Flips one of the snake's debug switches and says which way it went.</summary>
        private void AnimSnakeToggle(ref bool flag, string english, string turkish)
        {
            flag = !flag;
            animLastLabel = Loc.Pick("snake " + english + ": ", "yılan " + turkish + ": ")
                + OnOff(flag);
            if (AnimLabOpen)
            {
                RedrawAnimationLab();
            }
        }

        // ------------------------------------------------------------------ hidrolik pres
        //
        // "HIDROLIK PRES", every scenario, on a board of the lab's OWN - and it RUNS THE REAL RULES
        // on it: GameBoard.Compress and GameBoard.Expand, through their reporting overloads, exactly
        // as the power calls them. So the laminae, the push chain, the side that refuses, the axis
        // the corner opens on and the failure's footprint are the rules' own answers here, not a
        // drawing of them. What the lab fabricates is only the ARGUMENTS: the shape of the board,
        // what is standing in the way, and which turn of the countdown to hold.

        private enum AnimPressScene
        {
            Full4,
            Three,
            Two,
            One,
            Empty,
            Mixed,
            Stone,
            Idle1,
            Idle2,
            Idle3,
            Idle4,
            CleanRelease,
            PushOne,
            PushChain,
            PushOffBoard,
            BlockedGold,
            BlockedObsidian,
            Rerouted,
            Failure,
            FailureStone,
            DestroyedShut,
            Lifecycle,
            FailureLifecycle
        }

        /// <summary>The press scene playing, so pressing another one stops it.</summary>
        private Coroutine animPress;

        /// <summary>Which wait turn the lab is currently showing, so ADVANCE A TURN can step it and
        /// the pressure tick can be watched on its own rather than only in a whole lifecycle.
        /// </summary>
        private int animPressWaitTurn;

        private void AnimPress(AnimPressScene scene)
        {
            StopAnimPress();
            StopAnimSnake();
            StopAnimBossLift();
            StopAnimRot();
            StopAnimRaw();
            animPress = StartCoroutine(PressRoutine(scene));
        }

        private void StopAnimPress()
        {
            if (animPress != null)
            {
                StopCoroutine(animPress);
                animPress = null;
            }
            if (hydraulicPress != null)
            {
                hydraulicPress.Stop();
            }
            if (pressureVessel != null)
            {
                pressureVessel.Stop();
            }
            boardView.StopPress();
        }

        /// <summary>Which turn of the countdown an idle scene holds, or 0 when it is not an idle
        /// scene. The press is shut for TurnsCompressed turn-ends, so the marks a standing press
        /// shows run 1..3 and the fourth is the release's own.</summary>
        private static int AnimPressIdleTurn(AnimPressScene scene)
        {
            switch (scene)
            {
                case AnimPressScene.Idle1: return 1;
                case AnimPressScene.Idle2: return 2;
                case AnimPressScene.Idle3: return 3;
                case AnimPressScene.Idle4: return 4;
                default: return 0;
            }
        }

        private static bool AnimPressReleases(AnimPressScene scene)
        {
            switch (scene)
            {
                case AnimPressScene.CleanRelease:
                case AnimPressScene.PushOne:
                case AnimPressScene.PushChain:
                case AnimPressScene.PushOffBoard:
                case AnimPressScene.BlockedGold:
                case AnimPressScene.BlockedObsidian:
                case AnimPressScene.Rerouted:
                case AnimPressScene.Failure:
                case AnimPressScene.FailureStone:
                case AnimPressScene.Lifecycle:
                case AnimPressScene.FailureLifecycle:
                    return true;
                default:
                    return false;
            }
        }

        private IEnumerator PressRoutine(AnimPressScene scene)
        {
            RoundEngine round = session != null ? session.CurrentRound : null;
            if (round == null || round.Board == null || boardView == null)
            {
                animPress = null;
                yield break;
            }
            // Seven wide is enough for a long push chain to the rim with the press left of middle.
            int w = Mathf.Max(7, round.Board.Width);
            int h = Mathf.Max(7, round.Board.Height);
            var board = new GameBoard(w, h);
            List<int> cards = AnimBossCards();
            var anchor = new GridPos(2, h / 2 - 1);
            var reserved = new HashSet<GridPos>();
            AnimPressSetup(board, scene, cards, anchor, reserved);
            AnimRotFill(board, cards, AnimPressDensity(scene), reserved);
            boardView.Rebuild(board, MainBoardWorldSize, MainBoardCenter);
            yield return new WaitForSeconds(AnimBossBeat * 0.5f);

            // ---- THE SQUEEZE, through the real rules ----
            PressCompressionVisuals squeeze;
            Cube?[] swallowed = board.Compress(anchor, out squeeze);
            if (swallowed == null || squeeze == null)
            {
                animPress = null;
                yield break;
            }
            boardView.Refresh();
            boardView.Press.SetMemory(AnimPressMemory(squeeze, 0), AnimPressMemory(squeeze, 1),
                AnimPressMemory(squeeze, 2), AnimPressMemory(squeeze, 3));
            PlayPressScene(PressSqueezeSceneOf(squeeze));
            animLastLabel = AnimPressLabel(scene, squeeze, null);
            if (AnimLabOpen)
            {
                RedrawAnimationLab();
            }
            float guard = 0f;
            while (hydraulicPress != null && hydraulicPress.Busy && guard < 4f)
            {
                guard += Time.deltaTime;
                yield return null;
            }

            // ---- an IDLE scene holds the countdown where it is and stops there ----
            int idle = AnimPressIdleTurn(scene);
            if (idle > 0)
            {
                // TurnsLeft as the rules really have it on that turn. The power sets it mid-turn
                // and decrements at the END of that turn, so the states the player sees are
                // 4, 3, 2, 1 - four of them, one mark each.
                animPressWaitTurn = idle;
                boardView.Press.SetCountdown(Mathf.Max(1, 4 - idle + 1), 4);
                // A crease is a few pixels of recess, so an entry that "looks the same" as its
                // neighbour has to be able to prove which it is. The label says what is live.
                yield return null;
                animLastLabel = Loc.Pick("turn " + idle + " - ", idle + ". tur - ")
                    + boardView.Press.DebugState();
                if (AnimLabOpen)
                {
                    RedrawAnimationLab();
                }
                animPress = null;
                yield break;
            }
            // ---- BROKEN WHILE SHUT: no release at all, and the stored picture goes with it ----
            if (scene == AnimPressScene.DestroyedShut)
            {
                boardView.Press.SetCountdown(2, 4);
                yield return new WaitForSeconds(AnimBossBeat);
                var faces = new List<ClusterBurstView.Look>();
                Sprite tile;
                Color colour;
                faces.Add(boardView.TryCubeLook(anchor, CubeLookMaxAge, out tile, out colour)
                    ? new ClusterBurstView.Look { Tile = tile, Colour = colour }
                    : new ClusterBurstView.Look());
                // A second board, because the lab cannot take a cube off one any more than the
                // press can put one back - the same trick the raw scenes use.
                var after = new GameBoard(board.Width, board.Height);
                for (int x = 0; x < board.Width; x++)
                {
                    for (int y = 0; y < board.Height; y++)
                    {
                        var cell = new GridPos(x, y);
                        Cube? had = board.GetCube(cell);
                        if (had.HasValue && !cell.Equals(anchor))
                        {
                            after.SetCubeAt(cell, had.Value);
                        }
                    }
                }
                boardView.Rebuild(after, MainBoardWorldSize, MainBoardCenter);
                // The game's own line language: it was cleared, not opened.
                FlashCells(new List<GridPos> { anchor }, BlastColor, null, faces);
                animLastLabel = Loc.Pick(
                    "broken while shut: four cubes' worth of score, and the stored picture is gone",
                    "sıkışıkken kırıldı: dört küp puanı, saklanan resim gitti");
                if (AnimLabOpen)
                {
                    RedrawAnimationLab();
                }
                animPress = null;
                yield break;
            }
            if (!AnimPressReleases(scene))
            {
                animPress = null;
                yield break;
            }

            // ---- the turns it stands there, so a lifecycle really waits ----
            bool lifecycle = scene == AnimPressScene.Lifecycle
                || scene == AnimPressScene.FailureLifecycle;
            int turns = lifecycle ? 3 : 1;
            for (int turn = 1; turn <= turns; turn++)
            {
                boardView.Press.SetCountdown(4 - turn, 4);
                animLastLabel = Loc.Pick("shut, " + (4 - turn) + " turn(s) to go",
                    "sıkışık, " + (4 - turn) + " tur kaldı");
                if (AnimLabOpen)
                {
                    RedrawAnimationLab();
                }
                yield return new WaitForSeconds(lifecycle ? AnimBossBeat : AnimBossBeat * 0.6f);
            }

            // ---- whatever has to be standing in the way goes in now ----
            AnimPressObstacles(board, scene, cards, anchor);
            boardView.Refresh();
            if (AnimPressObstacleBeat(scene))
            {
                yield return new WaitForSeconds(AnimBossBeat * 0.6f);
            }

            // ---- THE RELEASE, through the real rules ----
            PressReleaseVisuals open;
            PressExpansion result = board.Expand(anchor, swallowed, out open);
            if (result == null || open == null)
            {
                animPress = null;
                yield break;
            }
            boardView.Refresh();
            PlayPressReleaseReport(open);
            animLastLabel = AnimPressLabel(scene, squeeze, open);
            if (AnimLabOpen)
            {
                RedrawAnimationLab();
            }
            // AND IT STAYS, like every other lab entry: the state worth looking at is the one the
            // release ENDS in - the 2x2 back, the cubes shoved, or the hole the failure left.
            animPress = null;
        }

        /// <summary>How full the lab board is around the press. A push scene lays its own chain, so
        /// the filler stays out of its way; a failure scene wants room to read.</summary>
        private static uint AnimPressDensity(AnimPressScene scene)
        {
            switch (scene)
            {
                case AnimPressScene.PushChain:
                case AnimPressScene.PushOffBoard:
                case AnimPressScene.Failure:
                case AnimPressScene.FailureStone:
                case AnimPressScene.FailureLifecycle:
                    return 0u;
                case AnimPressScene.CleanRelease:
                    return 12u;
                default:
                    return 26u;
            }
        }

        /// <summary>
        /// What stands in the four cells the press is about to take. The patch order the rules use
        /// is anchor, right, up, up-right - so "three cubes and a hole" means leaving ONE of those
        /// four empty, and which one is the lab's choice.
        /// </summary>
        private void AnimPressSetup(GameBoard board, AnimPressScene scene, List<int> cards,
            GridPos anchor, HashSet<GridPos> reserved)
        {
            var patch = new List<GridPos>
            {
                anchor,
                new GridPos(anchor.X + 1, anchor.Y),
                new GridPos(anchor.X, anchor.Y + 1),
                new GridPos(anchor.X + 1, anchor.Y + 1)
            };
            for (int i = 0; i < patch.Count; i++)
            {
                reserved.Add(patch[i]);
            }
            // The three cells it will want back, and the lane it may shove along: kept clear here so
            // only what a scene deliberately puts there is in the way.
            for (int i = 1; i < patch.Count; i++)
            {
                reserved.Add(patch[i]);
            }
            for (int x = anchor.X; x < board.Width; x++)
            {
                reserved.Add(new GridPos(x, anchor.Y));
                reserved.Add(new GridPos(x, anchor.Y + 1));
            }
            for (int y = anchor.Y; y < board.Height; y++)
            {
                reserved.Add(new GridPos(anchor.X, y));
                reserved.Add(new GridPos(anchor.X + 1, y));
            }
            int fill;
            switch (scene)
            {
                case AnimPressScene.Three: fill = 3; break;
                case AnimPressScene.Two: fill = 2; break;
                case AnimPressScene.One: fill = 1; break;
                case AnimPressScene.Empty: fill = 0; break;
                default: fill = 4; break;
            }
            for (int i = 0; i < fill; i++)
            {
                GridPos cell = patch[i];
                if (scene == AnimPressScene.Mixed)
                {
                    // FOUR DIFFERENT MATERIALS, fixed - this is the test that proves each lamina
                    // keeps its OWN colour. If the four come out the same the effect has failed,
                    // and a random fill could hide that by handing out four of the same card.
                    board.SetCubeAt(cell, AnimCardCube(cell.X, cell.Y, cards));
                    board.SetCubeKind(cell, AnimPressMixedKinds[i]);
                }
                else if (scene == AnimPressScene.Stone)
                {
                    // GOLD AND OBSIDIAN INSIDE the press: the rules store them like anything else,
                    // so their laminae have to go in as gold and obsidian.
                    board.SetCubeAt(cell, AnimCardCube(cell.X, cell.Y, cards));
                    board.SetCubeKind(cell, i % 2 == 0 ? CubeKind.Gold : CubeKind.Obsidian);
                }
                else
                {
                    board.SetCubeAt(cell, AnimCardCube(cell.X, cell.Y, cards));
                }
            }
        }

        /// <summary>What the press finds in its way when it opens. Laid AFTER the squeeze, which is
        /// exactly how it happens in a game: the player used the room.</summary>
        private void AnimPressObstacles(GameBoard board, AnimPressScene scene, List<int> cards,
            GridPos anchor)
        {
            var right = new GridPos(anchor.X + 1, anchor.Y);
            var up = new GridPos(anchor.X, anchor.Y + 1);
            var corner = new GridPos(anchor.X + 1, anchor.Y + 1);
            switch (scene)
            {
                case AnimPressScene.PushOne:
                    // ONE cube in the cell to the right: one shove, one cell.
                    board.SetCubeAt(right, AnimCardCube(right.X, right.Y, cards));
                    break;
                case AnimPressScene.PushChain:
                    // A run of cubes along the row, stopping short of the rim so they all land.
                    for (int x = right.X; x < board.Width - 1; x++)
                    {
                        var cell = new GridPos(x, anchor.Y);
                        board.SetCubeAt(cell, AnimCardCube(cell.X, cell.Y, cards));
                    }
                    break;
                case AnimPressScene.PushOffBoard:
                    // All the way TO the rim: the last one has nowhere to land and goes over.
                    for (int x = right.X; x < board.Width; x++)
                    {
                        var cell = new GridPos(x, anchor.Y);
                        board.SetCubeAt(cell, AnimCardCube(cell.X, cell.Y, cards));
                    }
                    break;
                case AnimPressScene.BlockedGold:
                case AnimPressScene.BlockedObsidian:
                    // A STRAIGHT side shut. There is no reroute for those in the rules, so this is
                    // the shape that detonates - which is the honest thing to show.
                    board.SetCubeAt(right, AnimCardCube(right.X, right.Y, cards));
                    var stone = new GridPos(anchor.X + 2, anchor.Y);
                    board.SetCubeAt(stone, AnimCardCube(stone.X, stone.Y, cards));
                    board.SetCubeKind(stone, scene == AnimPressScene.BlockedGold
                        ? CubeKind.Gold : CubeKind.Obsidian);
                    break;
                case AnimPressScene.Rerouted:
                    // THE ONE REROUTE IN THE POWER: the CORNER's horizontal is shut by gold, so the
                    // rules try its vertical and open that way instead.
                    board.SetCubeAt(corner, AnimCardCube(corner.X, corner.Y, cards));
                    var shut = new GridPos(anchor.X + 2, anchor.Y + 1);
                    board.SetCubeAt(shut, AnimCardCube(shut.X, shut.Y, cards));
                    board.SetCubeKind(shut, CubeKind.Gold);
                    break;
                case AnimPressScene.Failure:
                case AnimPressScene.FailureLifecycle:
                    // BOTH of the corner's axes shut: two refusals, and then the vessel fails.
                    board.SetCubeAt(corner, AnimCardCube(corner.X, corner.Y, cards));
                    var shutX = new GridPos(anchor.X + 2, anchor.Y + 1);
                    var shutY = new GridPos(anchor.X + 1, anchor.Y + 2);
                    board.SetCubeAt(shutX, AnimCardCube(shutX.X, shutX.Y, cards));
                    board.SetCubeKind(shutX, CubeKind.Gold);
                    board.SetCubeAt(shutY, AnimCardCube(shutY.X, shutY.Y, cards));
                    board.SetCubeKind(shutY, CubeKind.Obsidian);
                    break;
                case AnimPressScene.FailureStone:
                    // The same failure, RINGED with gold and obsidian, so what the blast takes is
                    // unmistakable: these are the two cubes nothing else in the game removes.
                    board.SetCubeAt(corner, AnimCardCube(corner.X, corner.Y, cards));
                    for (int x = anchor.X - 1; x <= anchor.X + 2; x++)
                    {
                        for (int y = anchor.Y - 1; y <= anchor.Y + 2; y++)
                        {
                            var cell = new GridPos(x, y);
                            if (!board.IsInside(cell) || cell.Equals(anchor)
                                || cell.Equals(corner))
                            {
                                continue;
                            }
                            bool ring = x < anchor.X || x > anchor.X + 1
                                || y < anchor.Y || y > anchor.Y + 1;
                            if (!ring)
                            {
                                continue;
                            }
                            board.SetCubeAt(cell, AnimCardCube(cell.X, cell.Y, cards));
                            board.SetCubeKind(cell, (x + y) % 2 == 0
                                ? CubeKind.Gold : CubeKind.Obsidian);
                        }
                    }
                    break;
                default:
                    break; // a clean release: nothing is in the way, which is the scene
            }
        }

        /// <summary>The four materials the mixed test presses, in patch order: red, blue, olive
        /// and gold. Four unmistakably different things, so "they all came out the same colour" is
        /// impossible to miss.</summary>
        private static readonly CubeKind[] AnimPressMixedKinds =
        {
            CubeKind.Fire, CubeKind.Water, CubeKind.Gangrene, CubeKind.Gold
        };

        private static bool AnimPressObstacleBeat(AnimPressScene scene)
        {
            return scene != AnimPressScene.CleanRelease;
        }

        /// <summary>A stored quadrant's colour for the shell's memory, off the lab's own report.
        /// </summary>
        private static Color AnimPressMemory(PressCompressionVisuals report, int index)
        {
            Cube? cube = index < report.Swallowed.Count ? report.Swallowed[index] : null;
            if (!cube.HasValue)
            {
                return new Color(0f, 0f, 0f, 0f);
            }
            Color paint = ViewUtil.CubeMaterialColor(cube.Value);
            return new Color(paint.r, paint.g, paint.b, 1f);
        }

        /// <summary>What the rules actually did, said in words under the board - so a scene that
        /// looks wrong can be checked against the report rather than against a memory of the brief.
        /// </summary>
        private static string AnimPressLabel(AnimPressScene scene, PressCompressionVisuals squeeze,
            PressReleaseVisuals open)
        {
            if (open == null)
            {
                return Loc.Pick(
                    squeeze.OccupiedCount + " of 4 quadrants held a cube; "
                        + (4 - squeeze.OccupiedCount) + " travelled as holes",
                    "4 bölmeden " + squeeze.OccupiedCount + " dolu; "
                        + (4 - squeeze.OccupiedCount) + " boşluk olarak saklandı");
            }
            if (open.Detonated)
            {
                int refused = 0;
                for (int i = 0; i < open.Tests.Count; i++)
                {
                    if (!open.Tests[i].Succeeded)
                    {
                        refused++;
                    }
                }
                return Loc.Pick(
                    "FAILED: " + refused + " side(s) refused, " + open.DetonatedCells.Count
                        + " cell(s) taken, no score",
                    "PATLADI: " + refused + " yön reddetti, " + open.DetonatedCells.Count
                        + " hücre gitti, puan yok");
            }
            string axis = open.DiagonalAxis.HasValue
                ? (open.DiagonalAxis.Value == PressAxis.Horizontal
                    ? Loc.Pick("horizontal", "yatay")
                    : Loc.Pick("vertical", "dikey"))
                : Loc.Pick("none needed", "gerek yok");
            return Loc.Pick(
                "opened: " + open.Pushes.Count + " cube(s) shoved, " + open.CubesPushedOff
                    + " over the edge, corner axis " + axis
                    + (open.Rerouted ? " (REROUTED)" : ""),
                "açıldı: " + open.Pushes.Count + " küp itildi, " + open.CubesPushedOff
                    + " kenardan taştı, köşe ekseni " + axis
                    + (open.Rerouted ? " (YÖN DEĞİŞTİ)" : ""));
        }

        /// <summary>
        /// ONE TURN ARRIVING, on the press already standing there. This is the entry to watch at
        /// 0.25x: the shell loads, the dimple bites, the seams contract, the new crease locks with
        /// a short strain running along it, the shadow tightens and it settles - all of it, rather
        /// than a light coming on.
        /// </summary>
        private void AnimPressAdvanceTurn()
        {
            if (boardView == null || !boardView.Press.Holds(AnimPressAnchor()))
            {
                animLastLabel = Loc.Pick("no press standing - play a wait entry first",
                    "ortada pres yok - önce bir bekleme girişi oynat");
                if (AnimLabOpen)
                {
                    RedrawAnimationLab();
                }
                return;
            }
            animPressWaitTurn = animPressWaitTurn >= 4 ? 1 : animPressWaitTurn + 1;
            boardView.Press.SetCountdown(Mathf.Max(1, 4 - animPressWaitTurn + 1), 4);
            animLastLabel = Loc.Pick("turn " + animPressWaitTurn + " - ",
                animPressWaitTurn + ". tur - ") + boardView.Press.DebugState();
            if (AnimLabOpen)
            {
                RedrawAnimationLab();
            }
        }

        /// <summary>Where the lab's own press stands.</summary>
        private GridPos AnimPressAnchor()
        {
            RoundEngine round = session != null ? session.CurrentRound : null;
            int h = round != null && round.Board != null ? Mathf.Max(7, round.Board.Height) : 7;
            return new GridPos(2, h / 2 - 1);
        }

        private void AnimPressToggle(ref bool flag, string english, string turkish)
        {
            flag = !flag;
            animLastLabel = Loc.Pick("press " + english + ": ", "pres " + turkish + ": ")
                + OnOff(flag);
            if (AnimLabOpen)
            {
                RedrawAnimationLab();
            }
        }

        // ------------------------------------------------------------------ parazit / konak kup
        //
        // "PARAZIT"'s HOST CUBE, on a board of the lab's own. The harness is driven through the
        // same seams the game drives it through, and the REFUSALS come from the real rules: the lab
        // calls GameBoard.DestroyCube / DestroyCubeForced on the host and the board writes down the
        // refusal itself, exactly as it does in a turn. What the lab fabricates is only the
        // ARGUMENTS - which cube is the host, which joker is riding it, and what tries it.

        private enum AnimHostScene
        {
            Blue,
            Red,
            Purple,
            Special,
            PassengerColours,
            Seating,
            IdlePulse,
            ResistDestroy,
            ForcedRight,
            ForcedUp,
            SweepPass,
            IdleObsidian,
            TearObsidian,
            LineHorizontal,
            LineVertical,
            PassengerLoss,
            Lifecycle,
            LifecycleObsidian,
            /// <summary>THE WITHERING, AT FOUR STRENGTHS. The one thing that turns a translucent
            /// film into a parasite is that the block under it is dying, so it has to be judgeable
            /// on its own: none at all, a little, the tuned value, and all the way.</summary>
            DrainNone,
            DrainThin,
            DrainMedium,
            DrainHeavy,
            /// <summary>ONE LAYER AT A TIME. Each of these leaves exactly one of the wrap's parts
            /// standing, which is the only way to see what that part is actually worth.</summary>
            MembraneOnly,
            PatchesOnly,
            FoldsOnly,
            NestOnly
        }

        private Coroutine animHost;

        /// <summary>Which passenger the colour test is showing, so pressing it again steps on.
        /// </summary>
        private int animHostPassenger;

        /// <summary>The def ids the passenger test cycles, so the core really has to carry an
        /// identity rather than one fixed accent.</summary>
        private static readonly string[] AnimHostPassengers =
        {
            "robot_supurge", "buzluk", "kazi_calismasi", "tamagotchi", "antimadde"
        };

        private void AnimHost(AnimHostScene scene)
        {
            StopAnimHost();
            StopAnimPress();
            StopAnimSnake();
            StopAnimBossLift();
            StopAnimRot();
            StopAnimRaw();
            if (scene == AnimHostScene.PassengerColours)
            {
                animHostPassenger = (animHostPassenger + 1) % AnimHostPassengers.Length;
            }
            animHost = StartCoroutine(HostRoutine(scene));
        }

        private void StopAnimHost()
        {
            if (animHost != null)
            {
                StopCoroutine(animHost);
                animHost = null;
            }
            boardView.StopParasite();
        }

        private static CubeKind AnimHostKind(AnimHostScene scene)
        {
            switch (scene)
            {
                case AnimHostScene.Red: return CubeKind.Fire;
                case AnimHostScene.IdleObsidian: return CubeKind.Obsidian;
                case AnimHostScene.TearObsidian: return CubeKind.Obsidian;
                case AnimHostScene.LifecycleObsidian: return CubeKind.Obsidian;
                case AnimHostScene.Purple: return CubeKind.Obsidian;
                case AnimHostScene.Special: return CubeKind.Gold;
                default: return CubeKind.Water;
            }
        }

        private IEnumerator HostRoutine(AnimHostScene scene)
        {
            // THE LAYER AND DRAIN TESTS ARE SET UP BEFORE THE HOST IS BUILT, because a piece the
            // switches turned off is a piece that is never rented. Like every other lab entry this
            // one HOLDS what it leaves behind: the "all layers back on" switch is the reset.
            AnimHostSetup(scene);
            RoundEngine round = session != null ? session.CurrentRound : null;
            if (round == null || round.Board == null || boardView == null)
            {
                animHost = null;
                yield break;
            }
            int w = Mathf.Max(7, round.Board.Width);
            int h = Mathf.Max(7, round.Board.Height);
            var board = new GameBoard(w, h);
            List<int> cards = AnimBossCards();
            var cell = new GridPos(w / 2, h / 2);
            var reserved = new HashSet<GridPos> { cell };
            board.SetCubeAt(cell, AnimCardCube(cell.X, cell.Y, cards));
            CubeKind kind = AnimHostKind(scene);
            if (kind != CubeKind.Normal)
            {
                board.SetCubeKind(cell, kind);
            }
            // THE RULES MARK IT, not the View: this is the same call the joker makes.
            board.SetCubeProtected(cell);
            AnimRotFill(board, cards, scene == AnimHostScene.SweepPass ? 34u : 22u, reserved);
            boardView.Rebuild(board, MainBoardWorldSize, MainBoardCenter);

            Cube? hostCube = board.GetCube(cell);
            var host = new ParasiteHostView.Host
            {
                Cell = cell,
                Look = hostCube.HasValue ? LookOf(hostCube.Value) : new ClusterBurstView.Look(),
                Passenger = new BoundJokerIdentity
                {
                    Bound = true,
                    InstanceId = 1,
                    DefId = AnimHostPassengers[animHostPassenger],
                    DisplayName = AnimHostPassengers[animHostPassenger]
                }
            };
            var live = new List<ParasiteHostView.Host> { host };
            boardView.Parasite.Sync(boardView, live);
            animLastLabel = AnimHostLabel(scene, host);
            if (AnimLabOpen)
            {
                RedrawAnimationLab();
            }
            // Let the seating play out - the harness locking on is its own beat.
            yield return new WaitForSeconds(ParasiteHostView.Style.SeatTotal + 0.1f);

            switch (scene)
            {
                case AnimHostScene.ResistDestroy:
                    // THE REAL RULE: the board refuses, and writes the refusal down itself.
                    board.HostRefusals.Clear();
                    board.DestroyCube(cell);
                    PlayHostRefusals(board);
                    break;
                case AnimHostScene.ForcedRight:
                case AnimHostScene.ForcedUp:
                    board.HostRefusals.Clear();
                    board.SetForcedStep(scene == AnimHostScene.ForcedRight
                        ? new GridPos(1, 0) : new GridPos(0, 1));
                    board.DestroyCubeForced(cell);
                    PlayHostRefusals(board);
                    break;
                case AnimHostScene.SweepPass:
                    boardView.Parasite.PlaySweepPass(cell);
                    animLastLabel = Loc.Pick("the sweep could not take this one",
                        "temizlik bunu alamadı");
                    break;
                case AnimHostScene.TearObsidian:
                case AnimHostScene.LineHorizontal:
                    FlashLine(board, cell.Y, true);
                    yield return new WaitForSeconds(0.12f);
                    PlayParasiteSeveranceScene(cell, true);
                    break;
                case AnimHostScene.LineVertical:
                    FlashLine(board, cell.X, false);
                    yield return new WaitForSeconds(0.12f);
                    PlayParasiteSeveranceScene(cell, false);
                    break;
                case AnimHostScene.PassengerLoss:
                    PlayParasiteSeveranceScene(cell, true);
                    break;
                case AnimHostScene.Lifecycle:
                case AnimHostScene.LifecycleObsidian:
                    yield return new WaitForSeconds(AnimBossBeat);
                    board.HostRefusals.Clear();
                    board.DestroyCube(cell);
                    PlayHostRefusals(board);
                    yield return new WaitForSeconds(ParasiteHostView.Style.ClampDuration + 0.3f);
                    board.HostRefusals.Clear();
                    board.SetForcedStep(new GridPos(1, 0));
                    board.DestroyCubeForced(cell);
                    PlayHostRefusals(board);
                    yield return new WaitForSeconds(ParasiteHostView.Style.ClampDuration + 0.5f);
                    FlashLine(board, cell.Y, true);
                    yield return new WaitForSeconds(0.12f);
                    PlayParasiteSeveranceScene(cell, true);
                    break;
                default:
                    break; // the still scenes: the harness standing there is the entry
            }
            animHost = null;
        }

        /// <summary>
        /// What a test scene switches before the host is built. The drain tiers force one value
        /// (Layers.DrainOverride); the "alone" scenes leave exactly ONE part of the wrap standing,
        /// which is the only honest way to ask what that part is worth - a layer judged with every
        /// other layer over it is being judged by the layers over it.
        /// </summary>
        private static void AnimHostSetup(AnimHostScene scene)
        {
            ParasiteHostView.Layers.AllOn();
            switch (scene)
            {
                case AnimHostScene.DrainNone:
                    ParasiteHostView.Layers.DrainOverride = 0f;
                    break;
                case AnimHostScene.DrainThin:
                    ParasiteHostView.Layers.DrainOverride = 0.3f;
                    break;
                case AnimHostScene.DrainMedium:
                    ParasiteHostView.Layers.DrainOverride = 0.6f;
                    break;
                case AnimHostScene.DrainHeavy:
                    ParasiteHostView.Layers.DrainOverride = 1f;
                    break;
                case AnimHostScene.MembraneOnly:
                    // The film and what it is taking out of the block - nothing on top of it.
                    AnimHostOnly(membrane: true, folds: false, patches: false, veins: false,
                        ribs: false, core: false);
                    break;
                case AnimHostScene.PatchesOnly:
                    AnimHostOnly(membrane: false, folds: false, patches: true, veins: false,
                        ribs: false, core: false);
                    break;
                case AnimHostScene.FoldsOnly:
                    AnimHostOnly(membrane: false, folds: true, patches: false, veins: false,
                        ribs: false, core: false);
                    break;
                case AnimHostScene.NestOnly:
                    AnimHostOnly(membrane: false, folds: false, patches: false, veins: false,
                        ribs: false, core: true);
                    break;
                default:
                    break;
            }
        }

        private static void AnimHostOnly(bool membrane, bool folds, bool patches, bool veins,
            bool ribs, bool core)
        {
            ParasiteHostView.Layers.ShowMembrane = membrane;
            ParasiteHostView.Layers.ShowWindows = membrane;
            ParasiteHostView.Layers.ShowFolds = folds;
            ParasiteHostView.Layers.ShowPatches = patches;
            ParasiteHostView.Layers.ShowVeins = veins;
            ParasiteHostView.Layers.ShowRibs = ribs;
            ParasiteHostView.Layers.ShowCore = core;
            ParasiteHostView.Layers.ShowEssence = core;
        }

        /// <summary>Hands the board's OWN refusal log to the harness - the same path the turn uses.
        /// </summary>
        private void PlayHostRefusals(GameBoard board)
        {
            IReadOnlyList<HostRefusal> refusals = board.HostRefusals.Refusals;
            for (int i = 0; i < refusals.Count; i++)
            {
                HostRefusal r = refusals[i];
                boardView.Parasite.PlayRefusal(new ParasiteHostView.Refusal
                {
                    Cell = r.Cell,
                    Kind = r.Kind,
                    Step = new Vector2(r.Step.X, r.Step.Y),
                    HasDirection = r.HasDirection
                });
            }
            animLastLabel = Loc.Pick(
                "the rules refused " + refusals.Count + " attempt(s) - the clasp gripped",
                "kurallar " + refusals.Count + " denemeyi reddetti - kenet sıktı");
            if (AnimLabOpen)
            {
                RedrawAnimationLab();
            }
        }

        private static string AnimHostLabel(AnimHostScene scene, ParasiteHostView.Host host)
        {
            return Loc.Pick(
                "host: " + ViewUtil.KindLabel(AnimHostKind(scene)) + " cube, passenger \""
                    + host.Passenger.DefId + "\"",
                "konak: " + ViewUtil.KindLabel(AnimHostKind(scene)) + " küp, yolcu \""
                    + host.Passenger.DefId + "\"");
        }

        private void AnimHostToggle(ref bool flag, string english, string turkish)
        {
            flag = !flag;
            animLastLabel = Loc.Pick("host " + english + ": ", "konak " + turkish + ": ")
                + OnOff(flag);
            if (AnimLabOpen)
            {
                RedrawAnimationLab();
            }
        }

        // ------------------------------------------------------------------ mapus

        // "MAPUS" IN THE LAB. Every scene puts up a board of its own and runs the boss's REAL
        // targeting on it (MapusBoss.RetargetOn - the same Choose the round calls), then hands
        // what it reported to the same seam the game uses. What the lab fabricates is only the
        // ARGUMENTS: the shape of the board, and which lines it leaves one cube from full. Which
        // cell gets sealed, whether the seal moved or held, and whether the cap released one are
        // the rules' answers.
        //
        // Like every other entry these HOLD what they end on, until RESET or closing the lab puts
        // the round back.

        private enum AnimMapusScene
        {
            Standing,
            EmptyBoard,
            BusyBoard,
            Spawn,
            Despawn,
            Move,
            HoldsThreeTurns,
            CapRelease,
            RowHeldAlone,
            ColumnHeldAlone,
            BothHeldAlone,
            WardenCheck,
            DepthIdle,
            Denied,
            WholeTurn,
            ColourStress
        }

        private Coroutine animMapus;

        /// <summary>The lab's own board for the seal - never the round's, so nothing here touches
        /// Core state.</summary>
        private GameBoard animMapusBoard;

        private MapusBoss animMapusBoss;

        private void AnimMapus(AnimMapusScene scene)
        {
            StopAnimMapus();
            StopAnimHost();
            StopAnimPress();
            StopAnimSnake();
            StopAnimBossLift();
            StopAnimRot();
            StopAnimRaw();
            animMapus = StartCoroutine(MapusRoutine(scene));
        }

        private void StopAnimMapus()
        {
            if (animMapus != null)
            {
                StopCoroutine(animMapus);
                animMapus = null;
            }
            animMapusBoard = null;
            animMapusBoss = null;
            if (boardView != null)
            {
                boardView.StopMapus();
            }
        }

        private IEnumerator MapusRoutine(AnimMapusScene scene)
        {
            RoundEngine round = session != null ? session.CurrentRound : null;
            if (round == null || round.Board == null || boardView == null)
            {
                animMapus = null;
                yield break;
            }
            int w = Mathf.Max(7, round.Board.Width);
            int h = Mathf.Max(7, round.Board.Height);
            var board = new GameBoard(w, h);
            List<int> cards = AnimBossCards();
            animMapusBoard = board;

            // THE ARGUMENTS THE LAB FABRICATES: how full the board is, and which lines it leaves
            // one cube short - which is what decides where the rules will want to seal.
            switch (scene)
            {
                case AnimMapusScene.EmptyBoard:
                    break;
                case AnimMapusScene.BusyBoard:
                case AnimMapusScene.ColourStress:
                    AnimRotFill(board, cards, 52u, null);
                    break;
                case AnimMapusScene.RowHeldAlone:
                    AnimMapusFillLine(board, cards, h / 2, true, -1);
                    break;
                case AnimMapusScene.ColumnHeldAlone:
                    AnimMapusFillLine(board, cards, w / 2, false, -1);
                    break;
                case AnimMapusScene.BothHeldAlone:
                    AnimMapusFillLine(board, cards, h / 2, true, w / 2);
                    AnimMapusFillLine(board, cards, w / 2, false, h / 2);
                    break;
                default:
                    AnimRotFill(board, cards, 26u, null);
                    break;
            }
            boardView.Rebuild(board, MainBoardWorldSize, MainBoardCenter);

            // The boss itself, with its own rng so a press is repeatable.
            var boss = new MapusBoss();
            animMapusBoss = boss;
            var rng = new SeededRandom(4021);
            AnimMapusStep(boss, board, rng);
            if (scene == AnimMapusScene.Despawn || scene == AnimMapusScene.Move
                || scene == AnimMapusScene.HoldsThreeTurns || scene == AnimMapusScene.CapRelease
                || scene == AnimMapusScene.WholeTurn)
            {
                // Let the first prison finish going up before anything is asked of it.
                yield return new WaitForSeconds(MapusSealView.Style.SpawnTotal + 0.25f);
            }

            switch (scene)
            {
                case AnimMapusScene.Despawn:
                    // Nothing sealed at all: the prison comes down and stays down.
                    boardView.Mapus.Sync(boardView, null, false);
                    break;
                case AnimMapusScene.Move:
                    // Fill the cell's own row right up, so somewhere else is now the worst place
                    // on the board and the rules move the seal there themselves.
                    AnimMapusFillLine(board, cards, (boss.SealedCell.Y + 2) % h, true, -1);
                    boardView.Rebuild(board, MainBoardWorldSize, MainBoardCenter);
                    AnimMapusStep(boss, board, rng);
                    break;
                case AnimMapusScene.HoldsThreeTurns:
                case AnimMapusScene.CapRelease:
                    for (int i = 0; i < 3; i++)
                    {
                        AnimMapusStep(boss, board, rng);
                        yield return new WaitForSeconds(1.1f);
                    }
                    break;
                case AnimMapusScene.WardenCheck:
                    // Nothing to do: the warden check runs itself, every few seconds.
                    break;
                case AnimMapusScene.DepthIdle:
                    break;
                case AnimMapusScene.Denied:
                    yield return new WaitForSeconds(MapusSealView.Style.SpawnTotal + 0.3f);
                    for (int i = 0; i < 3; i++)
                    {
                        boardView.Mapus.PlayDenied(boss.SealedCell);
                        animLastLabel = Loc.Pick("a block was dragged over it - refused",
                            "üstüne blok sürüklendi - reddedildi");
                        yield return new WaitForSeconds(0.55f);
                    }
                    break;
                case AnimMapusScene.WholeTurn:
                    AnimMapusStep(boss, board, rng);
                    yield return new WaitForSeconds(1.2f);
                    AnimMapusFillLine(board, cards, (boss.SealedCell.Y + 3) % h, true, -1);
                    boardView.Rebuild(board, MainBoardWorldSize, MainBoardCenter);
                    AnimMapusStep(boss, board, rng);
                    break;
                default:
                    break;
            }
            animMapus = null;
        }

        /// <summary>
        /// One turn of the boss, against the lab's own board: the REAL targeting, and its own
        /// report handed to the same seam the game uses.
        /// </summary>
        private void AnimMapusStep(MapusBoss boss, GameBoard board, IRandomSource rng)
        {
            MapusSealVisuals seal = boss.RetargetOn(board, rng);
            if (seal == null)
            {
                return;
            }
            if (!seal.HasSeal)
            {
                boardView.Mapus.Sync(boardView, null, seal.Released);
            }
            else
            {
                boardView.Mapus.Sync(boardView, new MapusSealView.Seal
                {
                    Cell = seal.Cell,
                    TurnsHeld = seal.TurnsHeld,
                    MaxTurns = seal.MaxTurns,
                    RowGaps = seal.RowGaps,
                    ColumnGaps = seal.ColumnGaps,
                    RowHeldAlone = seal.RowHeldByTheSealAlone,
                    ColumnHeldAlone = seal.ColumnHeldByTheSealAlone
                }, seal.Released);
            }
            animLastLabel = AnimMapusLabel(seal);
            if (AnimLabOpen)
            {
                RedrawAnimationLab();
            }
        }

        /// <summary>Fills a whole row or column but for one cell, so the rules have a line that is
        /// genuinely one cube from exploding to aim at. <paramref name="gapAt"/> below zero leaves
        /// the gap in the middle.</summary>
        private void AnimMapusFillLine(GameBoard board, List<int> cards, int line, bool row,
            int gapAt)
        {
            int span = row ? board.Width : board.Height;
            int from = row ? board.MinX : board.MinY;
            int hole = gapAt >= 0 ? gapAt : from + span / 2;
            for (int i = from; i < from + span; i++)
            {
                if (i == hole)
                {
                    continue;
                }
                var at = row ? new GridPos(i, line) : new GridPos(line, i);
                if (board.IsInside(at) && !board.GetCube(at).HasValue)
                {
                    board.SetCubeAt(at, AnimCardCube(at.X, at.Y, cards));
                }
            }
        }

        /// <summary>What the report actually contained - so the label says the rules' answer and
        /// not the scene's intention.</summary>
        private static string AnimMapusLabel(MapusSealVisuals seal)
        {
            if (!seal.HasSeal)
            {
                return seal.Released
                    ? Loc.Pick("the cap RELEASED the cell - open for a turn",
                        "sınır hücreyi BIRAKTI - bir tur açık")
                    : Loc.Pick("no seal this turn - too few free cells",
                        "bu tur mühür yok - boş hücre az");
            }
            string where = seal.Cell.X + "," + seal.Cell.Y;
            string held = " " + seal.TurnsHeld + "/" + seal.MaxTurns;
            string lines = " (" + Loc.Pick("row ", "satır ") + seal.RowGaps
                + Loc.Pick(", column ", ", sütun ") + seal.ColumnGaps + ")";
            string what = seal.Moved
                ? Loc.Pick("sealed ", "mühürledi ")
                : Loc.Pick("still holding ", "hâlâ tutuyor ");
            string alone = seal.RowHeldByTheSealAlone || seal.ColumnHeldByTheSealAlone
                ? Loc.Pick(" - THIS cell is holding the line", " - hattı tam da BU hücre tutuyor")
                : string.Empty;
            return what + where + held + lines + alone;
        }

        private void AnimMapusToggle(ref bool flag, string english, string turkish)
        {
            flag = !flag;
            animLastLabel = Loc.Pick("mapus " + english + ": ", "mapus " + turkish + ": ")
                + OnOff(flag);
            if (AnimLabOpen)
            {
                RedrawAnimationLab();
            }
        }

        // ------------------------------------------------------------------ raw / not reworked
        //
        // THE POINT OF THESE IS THAT THEY ARE PLAIN. Each one is either a mechanic the game really
        // has and nobody has drawn yet (a sealed cell, a bonus ground) - the snake, the press and
        // the parasite's host have all been through a pass since and have sections of their own -
        // or a look that exists but had no entry of its own (the overtime squeeze, the vignette,
        // the glow under the grid, the mirror world, the backdrop, the retro skin, a card being
        // picked up, the circuit cooking). Nothing here is an animation anyone designed: it is the
        // BEFORE, so a later pass has something honest to be held against.

        private enum AnimRawScene
        {
            /// <summary>"Mapus" and "Tılsım": cell states that are only a tint.</summary>
            CellStates
        }

        /// <summary>The raw scene playing, so pressing another one stops it.</summary>
        private Coroutine animRaw;

        /// <summary>The lab's own mirror board, torn down with the scene.</summary>
        private BoardView animRawMirrorBoard;

        private void AnimRawBoard(AnimRawScene scene)
        {
            StopAnimBossLift();
            StopAnimRot();
            StopAnimRaw();
            animRaw = StartCoroutine(RawBoardRoutine(scene));
        }

        private void StopAnimRaw()
        {
            if (animRaw != null)
            {
                StopCoroutine(animRaw);
                animRaw = null;
            }
            if (animRawMirrorBoard != null)
            {
                Destroy(animRawMirrorBoard.gameObject);
                animRawMirrorBoard = null;
            }
        }

        private IEnumerator RawBoardRoutine(AnimRawScene scene)
        {
            RoundEngine round = session != null ? session.CurrentRound : null;
            if (round == null || round.Board == null || boardView == null)
            {
                animRaw = null;
                yield break;
            }
            int w = Mathf.Max(7, round.Board.Width);
            int h = Mathf.Max(7, round.Board.Height);
            var board = new GameBoard(w, h);
            List<int> cards = AnimBossCards();
            var reserved = new HashSet<GridPos>();
            switch (scene)
            {
                default:
                    // The two cell states are the BOARD's own colours, so the cells are left empty
                    // and painted below - the rules make no sealed cell on a lab board.
                    for (int x = 1; x <= 4; x++)
                    {
                        reserved.Add(new GridPos(x, h / 2));
                        reserved.Add(new GridPos(x, h / 2 - 2));
                    }
                    break;
            }
            AnimRotFill(board, cards, 30u, reserved);
            boardView.Rebuild(board, MainBoardWorldSize, MainBoardCenter);
            if (scene == AnimRawScene.CellStates)
            {
                for (int x = 1; x <= 4; x++)
                {
                    boardView.PaintCellState(new GridPos(x, h / 2), true);
                    boardView.PaintCellState(new GridPos(x, h / 2 - 2), false);
                }
                animLastLabel = Loc.Pick(
                    "bonus ground (Tılsım) - still just a tint; Mapus has its own section now",
                    "bonus zemin (Tılsım) - hâlâ yalnız bir renk; Mapus'un artık kendi bölümü var");
            }
            yield return new WaitForSeconds(AnimBossBeat);
            yield return new WaitForSeconds(AnimRawWatch);
            animRaw = null;
            AnimResync();
        }

        /// <summary>How long a raw scene is left standing before the round's board comes back.</summary>
        private const float AnimRawWatch = 3.0f;

        /// <summary>"Uzatma": the squeeze on the board itself, at the knob's level.</summary>
        private void AnimRawPressure()
        {
            if (boardView == null || boardView.Board == null)
            {
                return;
            }
            int level = Mathf.Max(1, animOvertime);
            overtimePressure.SetState(true, level, level * OvertimePressureView.Style.TurnsPerStage,
                boardView.WorldRect, boardView.CellWorldSize,
                boardView.Board.Width, boardView.Board.Height);
            animLastLabel = Loc.Pick("overtime pressure, stage ", "uzatma basıncı, kademe ") + level;
            if (AnimLabOpen)
            {
                RedrawAnimationLab();
            }
        }

        /// <summary>"Uzatma": the screen's own vignette, which hangs off the camera.</summary>
        private void AnimRawVignette()
        {
            int level = Mathf.Max(1, animOvertime);
            overtimeVignette.SetActive(true);
            overtimeVignette.Creep(OvertimeVignetteView.Style.CreepPerContinue * level);
            overtimeVignette.SetPulse(1f);
            animLastLabel = Loc.Pick("overtime vignette, creep x", "uzatma vinyeti, sıkışma x") + level;
            if (AnimLabOpen)
            {
                RedrawAnimationLab();
            }
        }

        /// <summary>"Uzatma": the light under the grid's own lines.</summary>
        private void AnimRawGlow()
        {
            boardView.SetOvertimeGlow(Mathf.Max(1, animOvertime));
            animLastLabel = Loc.Pick("line glow, level ", "hat parıltısı, seviye ")
                + Mathf.Max(1, animOvertime);
            if (AnimLabOpen)
            {
                RedrawAnimationLab();
            }
        }

        /// <summary>
        /// "Öteki dünya": the two-board layout, which no run in the lab has. The round's own mirror
        /// is built from its state (RefreshMirrorWorld), so the lab puts up two boards of its OWN in
        /// the same places the real thing uses - the main one shrunk and lifted, the mirror under it
        /// - and takes them down again. It shows the layout; there is nothing else to show.
        /// </summary>
        private void AnimRawMirror()
        {
            StopAnimBossLift();
            StopAnimRot();
            StopAnimRaw();
            animRaw = StartCoroutine(RawMirrorRoutine());
        }

        private IEnumerator RawMirrorRoutine()
        {
            RoundEngine round = session != null ? session.CurrentRound : null;
            if (round == null || round.Board == null || boardView == null)
            {
                animRaw = null;
                yield break;
            }
            int w = Mathf.Max(6, round.Board.Width);
            int h = Mathf.Max(6, round.Board.Height);
            List<int> cards = AnimBossCards();
            var here = new GameBoard(w, h);
            var there = new GameBoard(w, h);
            AnimRotFill(here, cards, 38u, new HashSet<GridPos>());
            AnimRotFill(there, cards, 22u, new HashSet<GridPos>());
            boardView.Rebuild(here, MirrorBoardWorldSize, MainWorldCenter);
            var go = new GameObject("LabMirrorBoardView");
            go.transform.SetParent(transform, false);
            animRawMirrorBoard = go.AddComponent<BoardView>();
            animRawMirrorBoard.CardLookup = boardView.CardLookup;
            animRawMirrorBoard.Rebuild(there, MirrorBoardWorldSize, MirrorWorldCenter);
            animLastLabel = Loc.Pick("main world above, mirror below",
                "üstte ana dünya, altta ayna");
            yield return new WaitForSeconds(AnimRawWatch + 1f);
            animRaw = null;
            StopAnimRaw();
            AnimResync();
        }

        /// <summary>The backdrop on its own: everything the run draws is put away for a beat, so
        /// what is left on screen is the ground, its pool of light, the dither and the vignette.</summary>
        private void AnimRawBackdrop()
        {
            StopAnimRaw();
            animRaw = StartCoroutine(RawBackdropRoutine());
        }

        private IEnumerator RawBackdropRoutine()
        {
            SetRunPresentationVisible(false);
            animLastLabel = Loc.Pick("the backdrop, alone", "yalnız arka plan");
            if (AnimLabOpen)
            {
                RedrawAnimationLab();
            }
            yield return new WaitForSeconds(1.8f);
            SetRunPresentationVisible(true);
            if (AnimLabOpen)
            {
                // The joker strip draws over the panel, so the lab keeps it away (see OpenAnimationLab).
                jokerBar.SetVisible(false);
            }
            animRaw = null;
            AnimResync();
        }

        /// <summary>The retro skin by itself: the CRT overlay, the bit crush and the retro mix.</summary>
        private void AnimRawRetro()
        {
            animRetro = !animRetro;
            ApplyAnimRetroSkin();
            animLastLabel = Loc.Pick("retro skin: ", "retro deri: ") + OnOff(animRetro);
            if (AnimLabOpen)
            {
                RedrawAnimationLab();
            }
        }

        /// <summary>
        /// What a card in the hand does today, built from the only primitives it has: hover, a
        /// sorting boost, MoveTo and SnapTo. There is no pick-up weight, no tilt and no drop
        /// settle - which is the thing worth seeing.
        /// </summary>
        private void AnimRawCardFeel()
        {
            StopAnimRaw();
            animRaw = StartCoroutine(RawCardFeelRoutine());
        }

        private IEnumerator RawCardFeelRoutine()
        {
            CardVisual card = cardLayer != null ? cardLayer.VisualOfSlot(0) : null;
            if (card == null)
            {
                animLastLabel = Loc.Pick("no card in the hand", "elde kart yok");
                animRaw = null;
                yield break;
            }
            Vector2 home = card.HomePosition;
            card.SetHovered(true);
            card.SetSortingBoost(20);
            yield return new WaitForSeconds(0.3f);
            card.MoveTo(home + new Vector2(0f, 0.9f), 0.16f, null);
            yield return new WaitForSeconds(0.45f);
            card.MoveTo(home + new Vector2(1.5f, 2.1f), 0.32f, null);
            yield return new WaitForSeconds(0.6f);
            card.MoveTo(home, 0.2f, null);
            yield return new WaitForSeconds(0.35f);
            card.SetHovered(false);
            card.SetSortingBoost(0);
            card.SnapTo(home);
            animRaw = null;
        }

        /// <summary>"Devre" cooking at three and a half times the length, so the stages it goes
        /// through - heat, the band, the core, cracks, ash - can be told apart at all.</summary>
        private void AnimRawCircuitSlow()
        {
            IReadOnlyList<GridPos> path = AnimCircuitPath();
            boardView.DetonateCircuit();
            boardView.PlayCircuitHeat(AnimCircuitCubes(path), CircuitOverloadView.RuptureTime * 3.5f);
            animLastLabel = Loc.Pick("circuit cooking, 3.5x its own time",
                "devre pişmesi, kendi süresinin 3,5 katı");
            if (AnimLabOpen)
            {
                RedrawAnimationLab();
            }
        }

        /// <summary>Flips one of the rot's debug switches and says which way it went.</summary>
        private void AnimRotToggle(ref bool flag, string english, string turkish)
        {
            flag = !flag;
            animLastLabel = Loc.Pick("rot " + english + ": ", "kangren " + turkish + ": ")
                + OnOff(flag);
            if (AnimLabOpen)
            {
                RedrawAnimationLab();
            }
        }

        private static uint AnimHash(int x, int y)
        {
            unchecked
            {
                uint h = (uint)(x * 73856093) ^ (uint)(y * 19349663);
                h ^= h >> 15;
                h *= 2246822519u;
                h ^= h >> 13;
                h *= 3266489917u;
                h ^= h >> 16;
                return h;
            }
        }

        /// <summary>The run AnimClusterEveryCount has going, so pressing it again restarts it.</summary>
        private Coroutine animBurstSequence;

        /// <summary>Long enough for one blast's afterglow and debris to be gone before the next.
        /// </summary>
        private const float AnimBurstGap = 0.95f;

        /// <summary>
        /// "Patlama: N hücre" at every size the cells knob offers, smallest first, each played in
        /// the neutral orange and then in the element knob's colour - the whole acceptance sweep in
        /// one press. Every blast is the same FlashCells call the game makes.
        /// </summary>
        private void AnimClusterEveryCount()
        {
            StopAnimBurstSequence();
            animBurstSequence = StartCoroutine(ClusterEveryCountRoutine());
        }

        private void StopAnimBurstSequence()
        {
            if (animBurstSequence != null)
            {
                StopCoroutine(animBurstSequence);
                animBurstSequence = null;
            }
        }

        private IEnumerator ClusterEveryCountRoutine()
        {
            GameBoard board = AnimBoard();
            if (board == null)
            {
                animBurstSequence = null;
                yield break;
            }
            BlockElement element = AnimElement();
            int total = AnimCellCounts.Length * 2;
            for (int i = 0; i < AnimCellCounts.Length; i++)
            {
                for (int pass = 0; pass < 2; pass++)
                {
                    bool neutral = pass == 0;
                    FlashCells(AnimCells(AnimCellCounts[i]),
                        neutral ? BlastColor : ViewUtil.ElementColor(element), null,
                        AnimCubeFaces(neutral ? (BlockElement?)null : element));
                    string name = "N=" + AnimCellCounts[i] + "  "
                        + (neutral ? Loc.Pick("neutral", "nötr") : ViewUtil.ElementLabel(element));
                    // Over the board rather than over the middle: forty cells cover the middle.
                    Vector2 above = boardView.CellToWorld(new GridPos(AnimMiddleColumn(),
                        board.MinY + board.Height - 1)) + Vector2.up * boardView.CellWorldSize;
                    FloatingTextFx.Spawn(transform, above, name, Color.white, 50, 0.045f);
                    animLastLabel = Loc.Pick("blast: ", "patlama: ") + name
                        + "  (" + (i * 2 + pass + 1) + "/" + total + ")";
                    if (AnimLabOpen)
                    {
                        RedrawAnimationLab();
                    }
                    yield return new WaitForSeconds(AnimBurstGap);
                }
            }
            animBurstSequence = null;
        }

        /// <summary>
        /// The faces of a block of <paramref name="element"/> (null: a plain block), one per cube,
        /// by the rule the hand and the board draw that block's cubes with (ViewUtil.CardCubeTile)
        /// - so the lab blasts a water block made of water tiles, a fox of fox tiles and a
        /// "Hedefli" block with its one bullseye, never the default tile tinted a colour.
        /// FlashCells cycles through them over as many cells as the blast has.
        /// </summary>
        private List<ClusterBurstView.Look> AnimCubeFaces(BlockElement? element)
        {
            var faces = new List<ClusterBurstView.Look>();
            BlockCard card = AnimScratchCard(element);
            if (card == null || card.Shape == null)
            {
                return faces;
            }
            for (int i = 0; i < card.Shape.Cells.Count; i++)
            {
                Color tint;
                Sprite tile = ViewUtil.CardCubeTile(card, card.Shape, i, true, out tint);
                faces.Add(new ClusterBurstView.Look { Tile = tile, Colour = tint });
            }
            return faces;
        }

        /// <summary>
        /// "Patlama: N hücre" on a block of EVERY type, one after another - a plain block first,
        /// then every element the market sells - each in its own colour and made of its own
        /// tiles, with its name above the board. Walked from the enum, not the element knob, so a
        /// new block type shows up here the day it exists.
        /// </summary>
        private void AnimClusterEveryElement()
        {
            StopAnimBurstSequence();
            animBurstSequence = StartCoroutine(ClusterEveryElementRoutine());
        }

        private IEnumerator ClusterEveryElementRoutine()
        {
            GameBoard board = AnimBoard();
            if (board == null)
            {
                animBurstSequence = null;
                yield break;
            }
            var types = new List<BlockElement?> { null };
            foreach (BlockElement element in System.Enum.GetValues(typeof(BlockElement)))
            {
                // Kara delik is a trap a joker lays, never a block anyone owns.
                if (element != BlockElement.Void)
                {
                    types.Add(element);
                }
            }
            for (int i = 0; i < types.Count; i++)
            {
                BlockElement? element = types[i];
                FlashCells(AnimCells(), element.HasValue ? ViewUtil.ElementColor(element.Value)
                    : BlastColor, null, AnimCubeFaces(element));
                string name = element.HasValue ? ViewUtil.ElementLabel(element.Value)
                    : Loc.Pick("PLAIN", "DÜZ");
                Vector2 above = boardView.CellToWorld(new GridPos(AnimMiddleColumn(),
                    board.MinY + board.Height - 1)) + Vector2.up * boardView.CellWorldSize;
                FloatingTextFx.Spawn(transform, above, name, Color.white, 50, 0.045f);
                animLastLabel = Loc.Pick("blast: ", "patlama: ") + name
                    + "  (" + (i + 1) + "/" + types.Count + ")";
                if (AnimLabOpen)
                {
                    RedrawAnimationLab();
                }
                yield return new WaitForSeconds(AnimBurstGap);
            }
            animBurstSequence = null;
        }

        /// <summary>The run AnimFallingCubesEveryType has going, so pressing it again restarts the
        /// run instead of stacking a second one on top of it.</summary>
        private Coroutine animFallSequence;

        /// <summary>How long each type is given before the next one drops: long enough for its
        /// cubes to be clearly gone.</summary>
        private const float AnimFallGap = 0.9f;

        /// <summary>
        /// A defective block of EVERY type, one after another in the middle of the board, each with
        /// its name floating above it - a plain block first, then every element the market sells.
        ///
        /// All of them are the same SpawnFallingCubes call with a different card, which is the
        /// honest picture: the rules drop every type the same way, so the only thing that can
        /// differ on screen is how the view draws it. The elements are walked from the enum rather
        /// than from the element knob's list, so a new block type shows up here the day it exists.
        /// </summary>
        private void AnimFallingCubesEveryType()
        {
            StopAnimFallSequence();
            animFallSequence = StartCoroutine(FallingCubesEveryTypeRoutine());
        }

        private void StopAnimFallSequence()
        {
            if (animFallSequence != null)
            {
                StopCoroutine(animFallSequence);
                animFallSequence = null;
            }
        }

        private IEnumerator FallingCubesEveryTypeRoutine()
        {
            var types = new List<BlockElement?> { null };
            foreach (BlockElement element in System.Enum.GetValues(typeof(BlockElement)))
            {
                // Kara delik is a trap the joker lays for one round. It is never sold, so it can
                // never be smuggled, and a defective one cannot exist to fall.
                if (element != BlockElement.Void)
                {
                    types.Add(element);
                }
            }
            for (int i = 0; i < types.Count; i++)
            {
                BlockCard card = AnimScratchCard(types[i]);
                SpawnFallingCubes(boardView, AnimPlacedCells(card), card);
                string name = types[i].HasValue
                    ? ViewUtil.ElementLabel(types[i].Value)
                    : Loc.Pick("PLAIN", "DÜZ");
                Vector2 above = boardView.CellToWorld(
                    new GridPos(AnimMiddleColumn(), AnimMiddleRow() + 2));
                FloatingTextFx.Spawn(transform, above, name, Color.white, 50, 0.045f);
                animLastLabel = Loc.Pick("falling: ", "düşüyor: ") + name
                    + "  (" + (i + 1) + "/" + types.Count + ")";
                if (AnimLabOpen)
                {
                    RedrawAnimationLab();
                }
                yield return new WaitForSeconds(AnimFallGap);
            }
            animFallSequence = null;
        }

        /// <summary>Brings the joker strip back out from behind the panel so its own animation
        /// can be watched (see PlayAnimationRow), and says so when there is nothing in it.</summary>
        private bool AnimShowJokerBar()
        {
            jokerBar.SetVisible(true);
            if (session.Jokers.Count > 0)
            {
                return true;
            }
            animLastLabel = Loc.Pick("(hold a joker first - J)", "(önce bir joker al - J)");
            return false;
        }

        private void AnimPulseJoker()
        {
            if (AnimShowJokerBar())
            {
                jokerBar.PulseJoker(session.Jokers.Jokers[0].InstanceId);
            }
        }

        private void AnimSellJoker()
        {
            if (AnimShowJokerBar())
            {
                jokerBar.AnimateJokerSold(0, session);
            }
        }

        private bool AnimHasPower()
        {
            if (session.Powers.Count > 0)
            {
                return true;
            }
            animLastLabel = Loc.Pick("(hold a power first - P)", "(önce bir güç al - P)");
            return false;
        }

        private void AnimPulsePower()
        {
            if (AnimHasPower())
            {
                powerBar.PulsePower(session.Powers.Powers[0].InstanceId);
            }
        }

        private void AnimSellPower()
        {
            if (AnimHasPower())
            {
                powerBar.AnimatePowerSold(0, session);
            }
        }

        /// <summary>The market buy flights, which only exist while the market is on screen -
        /// the offer tiles they fly FROM are market view objects.</summary>
        private void AnimMarketBuy(MarketOfferKind kind)
        {
            if (session.Phase != GamePhase.Market)
            {
                animLastLabel = Loc.Pick("(open the market first)", "(önce marketi aç)");
                return;
            }
            IReadOnlyList<MarketOffer> offers = session.Market.Offers;
            for (int i = 0; i < offers.Count; i++)
            {
                if (offers[i].Kind != kind)
                {
                    continue;
                }
                if (kind == MarketOfferKind.Joker)
                {
                    marketView.PlayJokerBuyFx(i, new Vector2(0f, 4.2f));
                }
                else if (kind == MarketOfferKind.Power)
                {
                    marketView.PlayPowerBuyFx(i, new Vector2(-7.4f, 0f));
                }
                else
                {
                    marketView.PlayBuyFx(i);
                }
                sfx.Buy();
                return;
            }
        }

        private void AnimDeckSell()
        {
            if (!deckOverlay.IsOpen || session.OwnedCards.Count == 0)
            {
                animLastLabel = Loc.Pick("(open the deck overlay first)", "(önce deste ekranını aç)");
                return;
            }
            deckOverlay.PlaySellFx(session.OwnedCards[0]);
            sfx.Buy();
        }

        // ---- the composites: the same parts, in the same order the turn plays them ----

        private void AnimTurnLineClear()
        {
            FlashLineAtKnob(AnimBoard(), AnimMiddleRow(), true);
            sfx.Explode();
            ShakeForBlast(false, false, animCombo);
            if (animCombo >= 2)
            {
                SpawnComboPopup(animCombo);
            }
        }

        private void AnimTurnCleanSweep()
        {
            sfx.CleanSweep(1f + 0.12f * Mathf.Min(animSweeps - 1, 8));
            sfx.Flame();
            FlashLineAtKnob(AnimBoard(), AnimMiddleRow(), true);
            EmitSweepConfetti();
            ShakeForBlast(false, true, animCombo);
            SpawnSweepPopup();
        }

        private void AnimTurnDynamite()
        {
            FlashDynamite(DynamiteCenter(null));
            sfx.Explode();
            ShakeForBlast(true, false, animCombo);
            SpawnDynamitePopup();
        }

        /// <summary>The turn's real ordering when water is involved: the pre-explosion fall, then
        /// the boom, then the fall the boom caused (see FinalizePlacement).</summary>
        private void AnimTurnWaterBoom()
        {
            List<IReadOnlyList<WaterMove>> all = AnimWaterFrames(6);
            if (all.Count == 0)
            {
                return;
            }
            int split = all.Count / 2;
            var pre = new List<IReadOnlyList<WaterMove>>();
            var post = new List<IReadOnlyList<WaterMove>>();
            for (int i = 0; i < all.Count; i++)
            {
                (i < split ? pre : post).Add(all[i]);
            }
            boardView.PlayWaterAnimation(pre, delegate
            {
                FlashLineAtKnob(AnimBoard(), AnimMiddleRow(), true);
                sfx.Explode();
                ShakeForBlast(false, false, animCombo);
                boardView.PlayWaterAnimation(post, null);
            });
        }
    }
}
