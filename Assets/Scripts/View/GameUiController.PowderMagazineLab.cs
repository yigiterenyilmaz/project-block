// PURPOSE: The animation lab's "BARUT TEDARİKÇİSİ / POWDER MAGAZINE" section - every scene the
// magazine needs to be judged by (PowderMagazineView), on a board of the lab's own.
//
// The lab's rule holds: the scenes drive the REAL view through the same calls the game makes
// (Show, Prepare, Begin, with FlashLine for the line that sets a block off) and fabricate only the
// ARGUMENTS - where the dynamite stands, what shape its block is, and how many charges it has. The
// reports are built with a BarutTedarikcisiJoker's OWN arithmetic (its cap, its unit value against
// the round's real threshold), so a rebalanced joker shows its new numbers here untouched. The lab
// board's cubes are real dynamite cubes, so the casing material is the board's own, exactly as in
// a round. A detonation holds the block's cells blank after it goes (nothing in the lab really
// destroys them) and, like every lab entry, the scene holds what it ends on until RESET.
//
// The side-by-side draws the OLD glow system (the legacy PowderChargeView, on the untouched tile)
// beside the magazine at stages 1, 3 and 5 - the question it asks is whether the new one reads as
// powder loaded into a pack rather than as a block getting more orange.

using System.Collections;
using System.Collections.Generic;
using ProjectBlock.Core;
using UnityEngine;

namespace ProjectBlock.View
{
    partial class GameUiController
    {
        private enum AnimMagScene
        {
            Static0,
            Static1,
            Static2,
            Static3,
            Static4,
            Static5,
            Load1,
            Load2,
            Load3,
            Load4,
            Load5,
            GrainsOnly,
            IgnitionOnly,
            StrapHeatOnly,
            PressureOnly,
            Idle3,
            Idle4,
            Idle5,
            MaxLock,
            Detonate1,
            Detonate3,
            Detonate5,
            PreIgnitionSlow,
            Reward,
            SmallBlock,
            LargeBlock,
            Irregular,
            Stress,
            LoadHalf,
            LoadQuarter,
            DetonateHalf,
            DetonateQuarter,
            SideBySide
        }

        private Coroutine animMagazine;

        private bool animMagazineUsed;

        private readonly List<GridPos> animMagazineHeld = new List<GridPos>();

        private void AddPowderMagazineAnims()
        {
            AddAnimSub("jokers", "barut", "barut tedarikçisi - powder magazine",
                "barut tedarikçisi - barut şarjörü");
            AddMag(AnimMagScene.Static0, "1. Stage 0 static (a fresh, cold pack)",
                "1. Aşama 0 durağan (yeni, soğuk paket)");
            AddMag(AnimMagScene.Static1, "2. Stage 1 static - PRIMED", "2. Aşama 1 durağan - ASTARLI");
            AddMag(AnimMagScene.Static2, "3. Stage 2 static - LOADED", "3. Aşama 2 durağan - DOLU");
            AddMag(AnimMagScene.Static3, "4. Stage 3 static - PRESSURIZED",
                "4. Aşama 3 durağan - BASINÇLI");
            AddMag(AnimMagScene.Static4, "5. Stage 4 static - OVERLOADED",
                "5. Aşama 4 durağan - AŞIRI DOLU");
            AddMag(AnimMagScene.Static5, "6. Stage 5 static - FULL MAGAZINE",
                "6. Aşama 5 durağan - ŞARJÖR DOLU");
            AddMag(AnimMagScene.Load1, "7. 0 -> 1 load", "7. 0 -> 1 yükleme");
            AddMag(AnimMagScene.Load2, "8. 1 -> 2 load", "8. 1 -> 2 yükleme");
            AddMag(AnimMagScene.Load3, "9. 2 -> 3 load", "9. 2 -> 3 yükleme");
            AddMag(AnimMagScene.Load4, "10. 3 -> 4 load", "10. 3 -> 4 yükleme");
            AddMag(AnimMagScene.Load5, "11. 4 -> 5 load (and the lock)", "11. 4 -> 5 yükleme (ve kilit)");
            AddMag(AnimMagScene.GrainsOnly, "12. Powder grains only", "12. Yalnız barut taneleri");
            AddMag(AnimMagScene.IgnitionOnly, "13. Primer ignition only", "13. Yalnız primer ateşlemesi");
            AddMag(AnimMagScene.StrapHeatOnly, "14. Strap heat only", "14. Yalnız kayış ısısı");
            AddMag(AnimMagScene.PressureOnly, "15. Casing pressure only", "15. Yalnız gövde basıncı");
            AddMag(AnimMagScene.Idle3, "16. Stage 3 idle (ember drift)", "16. Aşama 3 bekleme (kor kayması)");
            AddMag(AnimMagScene.Idle4, "17. Stage 4 idle (pressure tick)", "17. Aşama 4 bekleme (basınç tıkı)");
            AddMag(AnimMagScene.Idle5, "18. Stage 5 idle (sequence + smoke)",
                "18. Aşama 5 bekleme (dizi + duman)");
            AddMag(AnimMagScene.MaxLock, "19. Max-reached lock", "19. Maksimum kilidi");
            AddMag(AnimMagScene.Detonate1, "20. Stage 1 detonation", "20. Aşama 1 patlama");
            AddMag(AnimMagScene.Detonate3, "21. Stage 3 detonation", "21. Aşama 3 patlama");
            AddMag(AnimMagScene.Detonate5, "22. Stage 5 detonation (full magazine)",
                "22. Aşama 5 patlama (dolu şarjör)");
            AddMag(AnimMagScene.PreIgnitionSlow, "23. Stage 5 pre-ignition, slow motion",
                "23. Aşama 5 ön ateşleme, ağır çekim");
            AddMag(AnimMagScene.Reward, "24. Reward payout (with its real value)",
                "24. Ödül (gerçek değeriyle)");
            AddMag(AnimMagScene.SmallBlock, "25. Small dynamite block (ember notches)",
                "25. Küçük dinamit bloğu (kor çentikleri)");
            AddMag(AnimMagScene.LargeBlock, "26. Large dynamite block", "26. Büyük dinamit bloğu");
            AddMag(AnimMagScene.Irregular, "27. Irregular shape", "27. Düzensiz şekil");
            AddMag(AnimMagScene.Stress, "28. Dense stress test (five packs, loading together)",
                "28. Yoğun stres testi (beş paket, birlikte yükleniyor)");
            AddMag(AnimMagScene.LoadHalf, "29. 0.5x charge", "29. 0.5x yükleme");
            AddMag(AnimMagScene.LoadQuarter, "30. 0.25x charge", "30. 0.25x yükleme");
            AddMag(AnimMagScene.DetonateHalf, "31. 0.5x detonation", "31. 0.5x patlama");
            AddMag(AnimMagScene.DetonateQuarter, "32. 0.25x detonation", "32. 0.25x patlama");
            AddMag(AnimMagScene.SideBySide,
                "ACCEPTANCE: old glow (left) vs powder magazine (right), stages 1 / 3 / 5",
                "KABUL: eski parıltı (sol) - barut şarjörü (sağ), aşama 1 / 3 / 5");

            AddMagToggle("group bounds", "grup sınırları",
                delegate { return PowderMagazineView.Layers.ShowGunpowderGroupBounds; },
                delegate (bool v) { PowderMagazineView.Layers.ShowGunpowderGroupBounds = v; });
            AddMagToggle("charge stage (dev label)", "şarj aşaması (geliştirici etiketi)",
                delegate { return PowderMagazineView.Layers.ShowChargeStage; },
                delegate (bool v) { PowderMagazineView.Layers.ShowChargeStage = v; });
            AddMagToggle("primer sockets", "primer yuvaları",
                delegate { return PowderMagazineView.Layers.ShowPrimerSockets; },
                delegate (bool v) { PowderMagazineView.Layers.ShowPrimerSockets = v; });
            AddMagToggle("primer states (dev label)", "primer durumları (geliştirici etiketi)",
                delegate { return PowderMagazineView.Layers.ShowPrimerStates; },
                delegate (bool v) { PowderMagazineView.Layers.ShowPrimerStates = v; });
            AddMagToggle("seam heat", "dikiş ısısı",
                delegate { return PowderMagazineView.Layers.ShowSeamHeat; },
                delegate (bool v) { PowderMagazineView.Layers.ShowSeamHeat = v; });
            AddMagToggle("strap tension", "kayış gerilimi",
                delegate { return PowderMagazineView.Layers.ShowStrapTension; },
                delegate (bool v) { PowderMagazineView.Layers.ShowStrapTension = v; });
            AddMagToggle("powder grains", "barut taneleri",
                delegate { return PowderMagazineView.Layers.ShowPowderGrains; },
                delegate (bool v) { PowderMagazineView.Layers.ShowPowderGrains = v; });
            AddMagToggle("soot mask", "kurum maskesi",
                delegate { return PowderMagazineView.Layers.ShowSootMask; },
                delegate (bool v) { PowderMagazineView.Layers.ShowSootMask = v; });
            AddMagToggle("pressure knots", "basınç düğümleri",
                delegate { return PowderMagazineView.Layers.ShowPressureKnots; },
                delegate (bool v) { PowderMagazineView.Layers.ShowPressureKnots = v; });
            AddMagToggle("heat distortion", "ısıl titreşim",
                delegate { return PowderMagazineView.Layers.ShowHeatDistortion; },
                delegate (bool v) { PowderMagazineView.Layers.ShowHeatDistortion = v; });
            AddMagToggle("detonation energy flow (louder)", "patlama enerji akışı (belirgin)",
                delegate { return PowderMagazineView.Layers.ShowDetonationEnergyFlow; },
                delegate (bool v) { PowderMagazineView.Layers.ShowDetonationEnergyFlow = v; });
            AddMagToggle("reward value (dev label)", "ödül değeri (geliştirici etiketi)",
                delegate { return PowderMagazineView.Layers.ShowRewardValue; },
                delegate (bool v) { PowderMagazineView.Layers.ShowRewardValue = v; });
            AddAnim("barut switch: ALL back on", "barut anahtarı: TÜMÜ geri açık", delegate
            {
                PowderMagazineView.Layers.AllOn();
                animLastLabel = Loc.Pick("every magazine switch back on", "tüm şarjör anahtarları açık");
                if (AnimLabOpen)
                {
                    RedrawAnimationLab();
                }
            });
        }

        private void AddMag(AnimMagScene scene, string en, string tr)
        {
            AddAnim("barut " + en, "barut " + tr, delegate { AnimMag(scene); });
        }

        private void AddMagToggle(string en, string tr, System.Func<bool> get, System.Action<bool> set)
        {
            AddAnim("barut switch: " + en, "barut anahtarı: " + tr, delegate
            {
                bool on = !get();
                set(on);
                animLastLabel = Loc.Pick("magazine " + en + ": ", "şarjör " + tr + ": ") + OnOff(on);
                if (AnimLabOpen)
                {
                    RedrawAnimationLab();
                }
            });
        }

        private void AnimMag(AnimMagScene scene)
        {
            StopAnimHost();
            StopAnimPress();
            StopAnimSnake();
            StopAnimBossLift();
            StopAnimRot();
            StopAnimRaw();
            animMagazineUsed = true;
            animMagazine = StartCoroutine(MagRoutine(scene));
        }

        /// <summary>Called from StopAnimHost, so every way the lab stops a scene stops this one.
        /// Only undoes what a magazine scene actually did.</summary>
        private void StopAnimMagazine()
        {
            if (animMagazine != null)
            {
                StopCoroutine(animMagazine);
                animMagazine = null;
            }
            if (!animMagazineUsed)
            {
                return;
            }
            animMagazineUsed = false;
            if (magazine != null)
            {
                magazine.Clear();
                magazine.PlaybackRate = 1f;
            }
            if (powder != null)
            {
                powder.Clear();
            }
            if (boardView != null && animMagazineHeld.Count > 0)
            {
                boardView.ReleaseCells(animMagazineHeld);
            }
            animMagazineHeld.Clear();
            PowderMagazineView.Layers.OnlyPhase = PowderMagazineView.LoadPhase.All;
        }

        // ------------------------------------------------------------------ blocks

        private struct MagBlock
        {
            public List<GridPos> Cells;
            public int Charges;
            public int CardId;
        }

        private static MagBlock MagShape(GridPos c, int kind, int charges, int cardId)
        {
            var cells = new List<GridPos>();
            switch (kind)
            {
                case 1: // small: one cube
                    cells.Add(c);
                    break;
                case 2: // large: 5 x 2
                    for (int x = -2; x <= 2; x++)
                    {
                        cells.Add(new GridPos(c.X + x, c.Y));
                        cells.Add(new GridPos(c.X + x, c.Y + 1));
                    }
                    break;
                case 3: // irregular: a long bottom run, a stem, a short top run
                    cells.Add(new GridPos(c.X - 1, c.Y - 1));
                    cells.Add(new GridPos(c.X, c.Y - 1));
                    cells.Add(new GridPos(c.X + 1, c.Y - 1));
                    cells.Add(new GridPos(c.X + 2, c.Y - 1));
                    cells.Add(new GridPos(c.X, c.Y));
                    cells.Add(new GridPos(c.X, c.Y + 1));
                    cells.Add(new GridPos(c.X + 1, c.Y + 1));
                    break;
                default: // the dense 3 x 2 pack the design is drawn against
                    for (int x = -1; x <= 1; x++)
                    {
                        cells.Add(new GridPos(c.X + x, c.Y));
                        cells.Add(new GridPos(c.X + x, c.Y + 1));
                    }
                    break;
            }
            return new MagBlock { Cells = cells, Charges = charges, CardId = cardId };
        }

        /// <summary>The joker's own arithmetic, for the lab's reports.</summary>
        private BarutTedarikcisiJoker MagArithmetic()
        {
            BarutTedarikcisiJoker owned = FindPowderJoker();
            return owned ?? new BarutTedarikcisiJoker();
        }

        private PowderVisuals MagReport(List<MagBlock> blocks, bool gained)
        {
            BarutTedarikcisiJoker joker = MagArithmetic();
            int threshold = session != null && session.CurrentRound != null
                ? session.CurrentRound.ScoreThreshold : 1000;
            int scale = session != null ? Mathf.Max(1, session.Config.Scoring.ScoreScale) : 1;
            var report = new PowderVisuals();
            for (int b = 0; b < blocks.Count; b++)
            {
                long value = (long)joker.PowderUnits(blocks[b].Charges)
                    * joker.UnitValue(threshold) * scale;
                for (int i = 0; i < blocks[b].Cells.Count; i++)
                {
                    report.Add(blocks[b].Cells[i], blocks[b].Charges, joker.MaxCharges, gained,
                        blocks[b].CardId, value);
                }
            }
            return report;
        }

        private PowderPayoutVisuals MagPayout(MagBlock block)
        {
            BarutTedarikcisiJoker joker = MagArithmetic();
            int threshold = session != null && session.CurrentRound != null
                ? session.CurrentRound.ScoreThreshold : 1000;
            int scale = session != null ? Mathf.Max(1, session.Config.Scoring.ScoreScale) : 1;
            var payout = new PowderPayoutVisuals();
            for (int i = 0; i < block.Cells.Count; i++)
            {
                payout.Add(block.Cells[i], block.Charges, joker.MaxCharges, block.CardId);
            }
            payout.Points = (long)block.Cells.Count * joker.PowderUnits(block.Charges)
                * joker.UnitValue(threshold) * scale;
            return payout;
        }

        /// <summary>Puts up a board of the lab's own with these dynamite blocks on it (and the
        /// usual clumps of ordinary blocks round them), and returns it.</summary>
        private GameBoard MagBoard(List<MagBlock> blocks)
        {
            RoundEngine round = session != null ? session.CurrentRound : null;
            int w = Mathf.Max(7, round != null && round.Board != null ? round.Board.Width : 7);
            int h = Mathf.Max(7, round != null && round.Board != null ? round.Board.Height : 7);
            var board = new GameBoard(w, h);
            var reserved = new HashSet<GridPos>();
            for (int b = 0; b < blocks.Count; b++)
            {
                for (int i = 0; i < blocks[b].Cells.Count; i++)
                {
                    GridPos c = blocks[b].Cells[i];
                    if (board.IsInside(c))
                    {
                        board.SetCubeAt(c, new Cube(CubeKind.Dynamite, blocks[b].CardId));
                        reserved.Add(c);
                    }
                }
            }
            AnimRotFill(board, AnimBossCards(), 16u, reserved);
            boardView.Rebuild(board, MainBoardWorldSize, MainBoardCenter);
            return board;
        }

        private static int MagStageOf(AnimMagScene scene)
        {
            switch (scene)
            {
                case AnimMagScene.Static0: return 0;
                case AnimMagScene.Static1: case AnimMagScene.Load1: case AnimMagScene.Detonate1: return 1;
                case AnimMagScene.Static2: case AnimMagScene.Load2: return 2;
                case AnimMagScene.Static3: case AnimMagScene.Load3: case AnimMagScene.Idle3:
                case AnimMagScene.Detonate3: case AnimMagScene.SmallBlock: case AnimMagScene.Irregular:
                case AnimMagScene.GrainsOnly: case AnimMagScene.IgnitionOnly:
                case AnimMagScene.StrapHeatOnly: case AnimMagScene.PressureOnly:
                case AnimMagScene.LoadHalf: case AnimMagScene.LoadQuarter:
                    return 3;
                case AnimMagScene.Static4: case AnimMagScene.Load4: case AnimMagScene.Idle4:
                case AnimMagScene.LargeBlock:
                    return 4;
                default:
                    return 5;
            }
        }

        private static bool MagIsLoad(AnimMagScene scene)
        {
            switch (scene)
            {
                case AnimMagScene.Load1: case AnimMagScene.Load2: case AnimMagScene.Load3:
                case AnimMagScene.Load4: case AnimMagScene.Load5: case AnimMagScene.GrainsOnly:
                case AnimMagScene.IgnitionOnly: case AnimMagScene.StrapHeatOnly:
                case AnimMagScene.PressureOnly: case AnimMagScene.LoadHalf: case AnimMagScene.LoadQuarter:
                case AnimMagScene.SmallBlock: case AnimMagScene.LargeBlock: case AnimMagScene.Irregular:
                    return true;
                default:
                    return false;
            }
        }

        private static bool MagIsDetonation(AnimMagScene scene)
        {
            switch (scene)
            {
                case AnimMagScene.Detonate1: case AnimMagScene.Detonate3: case AnimMagScene.Detonate5:
                case AnimMagScene.PreIgnitionSlow: case AnimMagScene.Reward:
                case AnimMagScene.DetonateHalf: case AnimMagScene.DetonateQuarter:
                    return true;
                default:
                    return false;
            }
        }

        private static float MagRate(AnimMagScene scene)
        {
            switch (scene)
            {
                case AnimMagScene.LoadHalf: case AnimMagScene.DetonateHalf: return 0.5f;
                case AnimMagScene.LoadQuarter: case AnimMagScene.DetonateQuarter:
                case AnimMagScene.PreIgnitionSlow: return 0.25f;
                default: return 1f;
            }
        }

        private static PowderMagazineView.LoadPhase MagPhase(AnimMagScene scene)
        {
            switch (scene)
            {
                case AnimMagScene.GrainsOnly: return PowderMagazineView.LoadPhase.Grains;
                case AnimMagScene.IgnitionOnly: return PowderMagazineView.LoadPhase.Ignition;
                case AnimMagScene.StrapHeatOnly: return PowderMagazineView.LoadPhase.StrapHeat;
                case AnimMagScene.PressureOnly: return PowderMagazineView.LoadPhase.Pressure;
                default: return PowderMagazineView.LoadPhase.All;
            }
        }

        // ------------------------------------------------------------------ the routine

        private IEnumerator MagRoutine(AnimMagScene scene)
        {
            if (session == null || boardView == null)
            {
                animMagazine = null;
                yield break;
            }
            EnsureMagazine();
            magazine.Clear();
            PowderMagazineView.Layers.OnlyPhase = MagPhase(scene);
            int w = Mathf.Max(7, session.CurrentRound != null && session.CurrentRound.Board != null
                ? session.CurrentRound.Board.Width : 7);
            int h = Mathf.Max(7, session.CurrentRound != null && session.CurrentRound.Board != null
                ? session.CurrentRound.Board.Height : 7);
            var centre = new GridPos(w / 2, h / 2);

            if (scene == AnimMagScene.SideBySide)
            {
                yield return MagSideBySide(w, h);
                animMagazine = null;
                yield break;
            }

            int stage = MagStageOf(scene);
            int shape = scene == AnimMagScene.SmallBlock ? 1
                : scene == AnimMagScene.LargeBlock ? 2
                : scene == AnimMagScene.Irregular ? 3
                : 0;
            var blocks = new List<MagBlock>();
            if (scene == AnimMagScene.Stress)
            {
                blocks.Add(MagShape(new GridPos(1, h - 2), 0, 1, 9101));
                blocks.Add(MagShape(new GridPos(w - 2, h - 2), 1, 2, 9102));
                blocks.Add(MagShape(new GridPos(1, h / 2 - 1), 1, 3, 9103));
                blocks.Add(MagShape(new GridPos(w / 2 + 1, h / 2 - 1), 0, 4, 9104));
                blocks.Add(MagShape(new GridPos(w / 2, 0), 0, 4, 9105));
            }
            else
            {
                blocks.Add(MagShape(centre, shape, MagIsLoad(scene) ? stage - 1 : stage, 9001));
            }
            GameBoard board = MagBoard(blocks);
            float rate = MagRate(scene);

            // ---- where the pack stands before the scene's event ----
            magazine.PlaybackRate = 1f;
            magazine.Show(MagReport(blocks, false), false);
            animLastLabel = Loc.Pick("magazine: stage " + (MagIsLoad(scene) ? stage - 1 : stage),
                "şarjör: aşama " + (MagIsLoad(scene) ? stage - 1 : stage));
            if (AnimLabOpen)
            {
                RedrawAnimationLab();
            }

            if (MagIsLoad(scene) || scene == AnimMagScene.Stress)
            {
                yield return new WaitForSeconds(0.6f);
                // One more turn of powder: a NEW report, as the joker writes one.
                for (int b = 0; b < blocks.Count; b++)
                {
                    MagBlock next = blocks[b];
                    next.Charges = Mathf.Min(next.Charges + 1, MagArithmetic().MaxCharges);
                    blocks[b] = next;
                }
                magazine.PlaybackRate = rate;
                magazine.Show(MagReport(blocks, true), true);
                animLastLabel = Loc.Pick("magazine: load to stage " + blocks[0].Charges
                    + (rate < 0.99f ? "  " + rate + "x" : ""),
                    "şarjör: aşama " + blocks[0].Charges + " yüklemesi"
                    + (rate < 0.99f ? "  " + rate + "x" : ""));
            }
            else if (scene == AnimMagScene.Idle3 || scene == AnimMagScene.Idle4
                || scene == AnimMagScene.Idle5)
            {
                yield return new WaitForSeconds(0.4f);
                for (int i = 0; i < 4; i++)
                {
                    magazine.ForceIdle();
                    yield return new WaitForSeconds(1.6f);
                }
            }
            else if (scene == AnimMagScene.MaxLock)
            {
                yield return new WaitForSeconds(0.6f);
                magazine.ForceLock();
                animLastLabel = Loc.Pick("magazine: FULL MAGAZINE LOCK", "şarjör: ŞARJÖR KİLİDİ");
            }
            else if (MagIsDetonation(scene))
            {
                if (scene == AnimMagScene.Reward)
                {
                    PowderMagazineView.Layers.ShowRewardValue = true;
                }
                yield return new WaitForSeconds(0.8f);
                MagBlock block = blocks[0];
                PowderPayoutVisuals payout = MagPayout(block);
                // Nothing in the lab really destroys the cubes: the board stands back from them,
                // as the round's own repaint would already have.
                boardView.HoldCells(block.Cells);
                animMagazineHeld.AddRange(block.Cells);
                magazine.PlaybackRate = rate;
                magazine.Prepare(payout);
                magazine.Show(new PowderVisuals(), false);
                int row = MagRailRow(block.Cells);
                FlashLine(board, row, true);
                var rows = new List<int> { row - board.MinY };
                var delays = new List<float>();
                for (int i = 0; i < payout.Count; i++)
                {
                    delays.Add(HazineBreakDelay(board, rows, null, payout.Cells[i]));
                }
                magazine.Begin(payout, delays);
                animLastLabel = Loc.Pick("magazine: stage " + stage + " cooks off, pays +" + payout.Points
                    + (rate < 0.99f ? "  " + rate + "x" : ""),
                    "şarjör: aşama " + stage + " patlıyor, öder +" + payout.Points
                    + (rate < 0.99f ? "  " + rate + "x" : ""));
            }
            if (AnimLabOpen)
            {
                RedrawAnimationLab();
            }
            animMagazine = null;
        }

        /// <summary>The row the block's line goes through: its longest run, as the view picks it.
        /// </summary>
        private static int MagRailRow(List<GridPos> cells)
        {
            var counts = new Dictionary<int, int>();
            int best = cells.Count > 0 ? cells[0].Y : 0;
            int bestCount = 0;
            for (int i = 0; i < cells.Count; i++)
            {
                int n;
                counts.TryGetValue(cells[i].Y, out n);
                counts[cells[i].Y] = ++n;
                if (n > bestCount)
                {
                    bestCount = n;
                    best = cells[i].Y;
                }
            }
            return best;
        }

        /// <summary>Old glow (left, on the untouched tile) against the magazine (right), at
        /// stages 1, 3 and 5 - one row each.</summary>
        private IEnumerator MagSideBySide(int w, int h)
        {
            var oldBlocks = new List<MagBlock>();
            var newBlocks = new List<MagBlock>();
            int[] stages = { 1, 3, 5 };
            for (int s = 0; s < stages.Length; s++)
            {
                int y = h - 2 - s * 2;
                var left = new List<GridPos> { new GridPos(0, y), new GridPos(1, y), new GridPos(2, y) };
                var right = new List<GridPos>
                {
                    new GridPos(w - 3, y), new GridPos(w - 2, y), new GridPos(w - 1, y)
                };
                oldBlocks.Add(new MagBlock { Cells = left, Charges = stages[s], CardId = 9201 + s });
                newBlocks.Add(new MagBlock { Cells = right, Charges = stages[s], CardId = 9301 + s });
            }
            var all = new List<MagBlock>(oldBlocks);
            all.AddRange(newBlocks);
            MagBoard(all);
            var rawCells = new List<GridPos>();
            for (int i = 0; i < oldBlocks.Count; i++)
            {
                rawCells.AddRange(oldBlocks[i].Cells);
            }
            magazine.SetRawCells(rawCells);
            magazine.Show(MagReport(newBlocks, false), false);
            EnsurePowder();
            powder.Show(MagReport(oldBlocks, false), false);
            animLastLabel = Loc.Pick("old glow (left) - powder magazine (right): stages 1, 3, 5",
                "eski parıltı (sol) - barut şarjörü (sağ): aşama 1, 3, 5");
            yield break;
        }
    }
}
