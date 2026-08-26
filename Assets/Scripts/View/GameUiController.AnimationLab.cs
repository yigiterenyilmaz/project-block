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

        /// <summary>Which way the gravity-arrow entry points next (see AnimGravityArrows).</summary>
        private int animGravityStep;

        private static readonly GridPos[] AnimGravityFlows =
        {
            new GridPos(-1, 0), new GridPos(1, 0), new GridPos(0, 1), new GridPos(0, -1)
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
                new AnimationLabView.Knob("combo streak", "kombo serisi", animCombo.ToString()),
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
            AddAnim("infection pips", "enfeksiyon işaretleri", AnimInfectionPips);
            AddAnim("circuit trace (Devre)", "devre izi (Devre)",
                delegate { boardView.ShowCircuit(AnimCells()); });
            AddAnim("quarantine wash (Karantina)", "karantina boyası (Karantina)",
                delegate
                {
                    AnimTintMarker(delegate
                    {
                        boardView.ShowQuarantine(AnimLines(true), AnimLines(false));
                    });
                });
            AddAnim("creature patch (Besleme)", "yaratık bölgesi (Besleme)",
                delegate
                {
                    AnimTintMarker(delegate { boardView.ShowCreature(AnimCells()); });
                });
            AddAnim("dolls (Matruşka)", "bebekler (Matruşka)", AnimDolls);
            AddAnim("doomed column (İstilacı)", "işaretli sütun (İstilacı)",
                delegate
                {
                    AnimTintMarker(delegate
                    {
                        boardView.ShowDoomedColumn(AnimMiddleColumn());
                    });
                });
            AddAnim("gravity arrows (cycles 4 ways)", "yerçekimi okları (4 yönü gezer)",
                AnimGravityArrows);
            AddAnim("clear board markers", "işaretleri temizle", AnimClearMarkers);
            AddAnim("mine shuffle dance (Mayın eşeği)", "mayın dansı (Mayın eşeği)", AnimMineDance);

            AddAnimHeader("blasts + shake", "patlama + sarsıntı");
            AddAnim("line clear ray: row", "satır ışını",
                delegate { FlashLine(AnimBoard(), AnimMiddleRow(), true); });
            AddAnim("line clear ray: column", "sütun ışını",
                delegate { FlashLine(AnimBoard(), AnimMiddleColumn(), false); });
            AddAnim("line clear ray: plus (row + column)", "artı ışını (satır + sütun)",
                AnimPlusBlast);
            AddAnim("blast: N cells (cells knob)", "patlama: N hücre (hücre ayarı)",
                delegate { FlashCells(AnimCells(), BlastColor, 4); });
            AddAnim("blast: element colour", "patlama: element rengi",
                delegate { FlashCells(AnimCells(), ViewUtil.ElementColor(AnimElement()), 6); });
            AddAnim("lifted cells (cold, a boss took them)", "kaldırılan hücreler (soğuk)",
                delegate { FlashCells(AnimCells(), LiftedColor, 3, true); });
            AddAnim("clean sweep: board flash + confetti", "temizlik: alan ışığı + yağmur",
                delegate { EmitSweepConfetti(); });
            AddAnim("dynamite: board flash + smoke", "dinamit: alan ışığı + duman", FlashDynamite);
            AddAnim("dynamite smoke alone", "dinamit dumanı (tek başına)", PlayDynamiteSmoke);
            AddAnim("power blast", "güç patlaması",
                delegate { PlayPowerBlast(AnimCells()); });
            AddAnim("süpürge delayed blast", "süpürge gecikmeli patlama",
                delegate { StartCoroutine(SupurgeBlastRoutine(AnimCells())); });
            AddAnim("infection detonation (green)", "enfeksiyon patlaması (yeşil)",
                delegate { FlashCells(AnimCells(), new Color(0.25f, 0.95f, 0.4f), 8); });
            AddAnim("falling cubes (defective block)", "düşen küpler (defolu blok)",
                AnimFallingCubes);
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
        private List<GridPos> AnimCells()
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
            int want = Mathf.Min(AnimCellCount(), ranked.Count);
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

        /// <summary>A one-entry row or column list, for the markers that take line indices.</summary>
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
            var cells = new List<GridPos>
            {
                new GridPos(0, 0), new GridPos(0, 1), new GridPos(1, 0)
            };
            var elements = new List<BlockElement> { AnimElement() };
            // A negative id keeps it clear of every real card in the run; nothing in the View
            // looks a card up by id, so this only ever picks its colour and its label.
            return new BlockCard(-4242, BlockShape.FromCells(cells), elements);
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

        private void AnimDolls()
        {
            List<GridPos> cells = AnimCells();
            var sizes = new List<int>(cells.Count);
            for (int i = 0; i < cells.Count; i++)
            {
                sizes.Add(1 + (i % 4)); // one of each rung of the 1-2-4-8 ladder
            }
            boardView.ShowDolls(cells, sizes);
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
        /// Walks the gravity arrows through all four directions, one per play, ending on the
        /// default. Cycling rather than showing the round's real flow, because the real flow is
        /// (0,-1) on nearly every board and ShowGravity draws NOTHING for it - so an entry that
        /// asked the board would have looked broken on every arena but a "Kütleçekim merkezi" one.
        /// </summary>
        private void AnimGravityArrows()
        {
            boardView.ShowGravity(AnimGravityFlows[animGravityStep]);
            animGravityStep = (animGravityStep + 1) % AnimGravityFlows.Length;
        }

        private void AnimClearMarkers()
        {
            // The marker-object overlays clear on their own...
            boardView.ShowInfections(null);
            boardView.ShowCircuit(null);
            boardView.ShowDolls(null, null);
            boardView.ShowGravity(new GridPos(0, -1)); // the default draws no arrows
            animGravityStep = 0;
            boardView.ClearPreview();
            // ...the three tinted ones need the repaint (see AnimTintMarker).
            AnimTintMarker(delegate
            {
                boardView.ShowQuarantine(null, null);
                boardView.ShowCreature(null);
                boardView.ShowDoomedColumn(null);
            });
        }

        private void AnimMineDance()
        {
            GameBoard board = AnimBoard();
            if (board == null)
            {
                return;
            }
            // A path that walks the middle band, so the cover is always somewhere visible.
            var path = new List<GridPos>();
            int y = AnimMiddleRow();
            for (int i = 0; i < 6; i++)
            {
                int x = board.MinX + (i * 2) % Mathf.Max(1, board.Width);
                path.Add(new GridPos(x, y));
            }
            mineShuffle.Play(boardView, board, path);
        }

        /// <summary>Both rays at once, which is how a turn that completes a row and a column at
        /// the same time reads: two waves leaving the same middle at the same instant.</summary>
        private void AnimPlusBlast()
        {
            FlashLine(AnimBoard(), AnimMiddleRow(), true);
            FlashLine(AnimBoard(), AnimMiddleColumn(), false);
        }

        private void AnimFallingCubes()
        {
            SpawnFallingCubes(boardView, AnimCells(), AnimScratchCard());
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
            FlashLine(AnimBoard(), AnimMiddleRow(), true);
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
            FlashLine(AnimBoard(), AnimMiddleRow(), true);
            EmitSweepConfetti();
            ShakeForBlast(false, true, animCombo);
            SpawnSweepPopup();
        }

        private void AnimTurnDynamite()
        {
            FlashDynamite();
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
                FlashLine(AnimBoard(), AnimMiddleRow(), true);
                sfx.Explode();
                ShakeForBlast(false, false, animCombo);
                boardView.PlayWaterAnimation(post, null);
            });
        }
    }
}
