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
            MatryoshkaView.Layers.AllOn();
            PhaseFoldView.Layers.AllOn();
            CryoSublimationView.Layers.AllOn();
            MomentumPeelView.Layers.AllOn();
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
            AddAnim("Kangren: a line dies, the rot jumps to the edge (old cold mark)",
                "kangren: satır ölür, kangren kenara atlar (eski soğuk iz)",
                delegate { AnimBossLift(AnimBossScene.Gangrene); });
            AddAnim("thrown off by a boss's move: the next of 8 directions each press (cells knob)",
                "boss hareketiyle atılma: her basışta sıradaki yön, 8 yön (hücre ayarı)",
                AnimPeelDirection);
            AddAnim("thrown off by a boss's move: riding into holes (escalator, a holed arena)",
                "boss hareketiyle atılma: deliğe çıkış (merdiven, delikli alan)",
                delegate { AnimBossLift(AnimBossScene.Holes); });
            AddAnim("thrown off by a boss's move: blocked target (staged)",
                "boss hareketiyle atılma: engelli hedef (sahnelenmiş)",
                AnimPeelBlocked);
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

        /// <summary>The three bosses that take cubes off WITHOUT them vanishing where they stood,
        /// and so keep the old cold mark (LiftCells) rather than a removal variant.</summary>
        private enum AnimBossScene
        {
            Escalator,
            Centrifuge,
            Gangrene,
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
        /// game's own sequence: the board changes, it is repainted, and LiftCells plays on exactly
        /// the cells the boss reported.
        ///
        ///   ESCALATOR   the real ShiftRowsUp: every row rides up one and the top row's cubes are
        ///               torn off over the top edge (PlayForcedExit, with the motions Core wrote).
        ///   CENTRIFUGE  the real FlingCubesOutward: every cube one cell further from the middle,
        ///               the rim's torn off outward - each along its own step, eight ways.
        ///   HOLES       the real ShiftRowsUp on an arena with holes in its top row: the cubes
        ///               under them ride in and have no ground; the top row's go over the edge.
        ///   GANGRENE    STAGED, because the rot's jump is Core's alone (InfectFullLines is
        ///               internal): the rot takes the last cell of its row, the row dies, and every
        ///               cube in the nearer edge row turns; next turn the same with its column and
        ///               the nearer edge column. The cells that turn are the ones the rule would name;
        ///               what the lab board cannot show is the dead line's wash, which only Core marks.
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
                List<GridPos> lifted = AnimBossTurn(board, scene, turn, motions);
                boardView.Refresh();
                if (scene == AnimBossScene.Gangrene)
                {
                    LiftCells(lifted, LiftedColor);
                }
                else
                {
                    // The seam the game hands a moving board's report to, with the motions the
                    // real board code wrote.
                    PlayForcedExit(lifted, motions);
                }
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
                case AnimBossScene.Centrifuge:
                {
                    // A few cubes on the rim for the first fling and more on the ring inside it for
                    // the second. Sparse on the rim, as in the round: every turn empties it.
                    bool rim = x == 0 || y == 0 || x == w - 1 || y == h - 1;
                    bool ring = !rim && (x == 1 || y == 1 || x == w - 2 || y == h - 2);
                    filled = roll < (rim ? 30u : ring ? 55u : 35u);
                    break;
                }
                default:
                {
                    // The rot: one patch, a whole row but one cell and a whole column but one.
                    int column = w - 3;
                    const int row = 2;
                    if (x == column && (y == row || y == h - 2))
                    {
                        return null; // where the rot goes next, one turn each
                    }
                    if (y == row || x == column)
                    {
                        return new Cube(CubeKind.Gangrene, GameBoard.GangreneCardId);
                    }
                    // Full edges for the jump to turn - each with a gap, so the edge line is not
                    // taken whole and does not die in its turn.
                    if (y == 0)
                    {
                        filled = x != 1 && x != w - 2;
                    }
                    else if (x == w - 1)
                    {
                        filled = y != h - 3;
                    }
                    else
                    {
                        filled = roll < 38u;
                    }
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
        /// as lifted - exactly what the game hands LiftCells.</summary>
        private static List<GridPos> AnimBossTurn(GameBoard board, AnimBossScene scene, int turn,
            List<LiftMotion> motions)
        {
            switch (scene)
            {
                case AnimBossScene.Escalator:
                case AnimBossScene.Holes:
                    return board.ShiftRowsUp(motions);
                case AnimBossScene.Centrifuge:
                    return board.FlingCubesOutward(motions);
                default:
                {
                    int column = board.MinX + board.Width - 3;
                    var rot = new Cube(CubeKind.Gangrene, GameBoard.GangreneCardId);
                    if (turn == 0)
                    {
                        // The rot takes the last cell of its row: the row dies, and the infection
                        // jumps to the nearer horizontal edge - the bottom one.
                        board.SetCubeAt(new GridPos(column, board.MinY + 2), rot);
                        return AnimRotEdge(board, true, board.MinY);
                    }
                    // Then the last cell of its column: that dies too, and the jump goes to the
                    // nearer vertical edge - the right one.
                    board.SetCubeAt(new GridPos(column, board.MinY + board.Height - 2), rot);
                    return AnimRotEdge(board, false, board.MinX + board.Width - 1);
                }
            }
        }

        /// <summary>The rot's jump, staged: every cube in an edge line that the rot can take turns
        /// where it stands. Converts only - an empty cell stays empty (see GameBoard.Gangrene).</summary>
        private static List<GridPos> AnimRotEdge(GameBoard board, bool row, int line)
        {
            var turned = new List<GridPos>();
            int count = row ? board.Width : board.Height;
            for (int i = 0; i < count; i++)
            {
                GridPos pos = row ? new GridPos(board.MinX + i, line) : new GridPos(line, board.MinY + i);
                Cube? cube = board.GetCube(pos);
                if (cube.HasValue && cube.Value.Kind != CubeKind.Gangrene
                    && CubeRules.IsExternallyDestructible(cube.Value))
                {
                    board.SetCubeKind(pos, CubeKind.Gangrene);
                    turned.Add(pos);
                }
            }
            return turned;
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
