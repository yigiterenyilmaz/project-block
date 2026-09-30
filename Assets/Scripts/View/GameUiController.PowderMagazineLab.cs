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
// The list is the corrective pass's A-V: the six stages standing, the five loads, each light
// source ALONE (primers, pockets, underglow, seam heat - three packs at stages 1 / 3 / 5, so the
// growth is the thing being judged - then the pressure pulse and the heat haze on a full pack), the
// stage 5 idle, the lock, the cook-off slowed down and at speed, and the before/after. The "only"
// scenes switch the other FEATURES off and put them back when the scene is stopped. The OLD side of
// the before/after is the previous pass (DynamiteCasing's _OldLook: crisp seam hairlines, the whole
// cube wobbling, no pockets) drawn by the same view, so the only difference is the language.

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
            PrimersOnly,
            PocketsOnly,
            UnderglowOnly,
            SeamHeatOnly,
            PressurePulseOnly,
            HeatDistortionOnly,
            Idle5,
            MaxLock,
            DetonationPrep5,
            Explosion5,
            SideBySide,
            GrainsBeat,
            IgnitionBeat,
            StrapHeatBeat,
            SmallBlock,
            LargeBlock,
            Irregular,
            Stress,
            LoadQuarter,
            Detonate1,
            Detonate3
        }

        private Coroutine animMagazine;

        private bool animMagazineUsed;

        /// <summary>The features as they were before an "only" scene switched the rest off.</summary>
        private bool[] animMagazineFeatures;

        private readonly List<GridPos> animMagazineHeld = new List<GridPos>();

        private void AddPowderMagazineAnims()
        {
            AddAnimSub("jokers", "barut", "barut tedarikçisi - powder magazine",
                "barut tedarikçisi - barut şarjörü");
            AddMag(AnimMagScene.Static0, "A. Stage 0 static (a fresh, cold pack)",
                "A. Aşama 0 durağan (yeni, soğuk paket)");
            AddMag(AnimMagScene.Static1, "B. Stage 1 static - one hot point",
                "B. Aşama 1 durağan - tek sıcak nokta");
            AddMag(AnimMagScene.Static2, "C. Stage 2 static - two pockets",
                "C. Aşama 2 durağan - iki cep");
            AddMag(AnimMagScene.Static3, "D. Stage 3 static - three, not yet meeting",
                "D. Aşama 3 durağan - üç cep, henüz birleşmiyor");
            AddMag(AnimMagScene.Static4, "E. Stage 4 static - pockets touching",
                "E. Aşama 4 durağan - cepler değiyor");
            AddMag(AnimMagScene.Static5, "F. Stage 5 static - one connected fill",
                "F. Aşama 5 durağan - bağlı tek dolgu");
            AddMag(AnimMagScene.Load1, "G. 0 -> 1 load", "G. 0 -> 1 yükleme");
            AddMag(AnimMagScene.Load2, "H. 1 -> 2 load", "H. 1 -> 2 yükleme");
            AddMag(AnimMagScene.Load3, "I. 2 -> 3 load", "I. 2 -> 3 yükleme");
            AddMag(AnimMagScene.Load4, "J. 3 -> 4 load", "J. 3 -> 4 yükleme");
            AddMag(AnimMagScene.Load5, "K. 4 -> 5 load (and the lock)", "K. 4 -> 5 yükleme (ve kilit)");
            AddMag(AnimMagScene.PrimersOnly, "L. Primer system only (stages 1 / 3 / 5)",
                "L. Yalnız primer sistemi (aşama 1 / 3 / 5)");
            AddMag(AnimMagScene.PocketsOnly, "M. Internal amber pockets only (stages 1 / 3 / 5)",
                "M. Yalnız iç kehribar cepler (aşama 1 / 3 / 5)");
            AddMag(AnimMagScene.UnderglowOnly, "N. Strap underglow only (stages 1 / 3 / 5)",
                "N. Yalnız kayış altı ışığı (aşama 1 / 3 / 5)");
            AddMag(AnimMagScene.SeamHeatOnly, "O. Seam heat only (stages 1 / 3 / 5)",
                "O. Yalnız dikiş ısısı (aşama 1 / 3 / 5)");
            AddMag(AnimMagScene.PressurePulseOnly, "P. Pressure pulse only (stage 5, repeated)",
                "P. Yalnız basınç nabzı (aşama 5, tekrarlı)");
            AddMag(AnimMagScene.HeatDistortionOnly, "Q. Heat distortion only (stage 5)",
                "Q. Yalnız ısı kırılması (aşama 5)");
            AddMag(AnimMagScene.Idle5, "R. Stage 5 idle (natural rhythm)",
                "R. Aşama 5 bekleme (doğal ritim)");
            AddMag(AnimMagScene.MaxLock, "S. Max charge lock", "S. Maksimum şarj kilidi");
            AddMag(AnimMagScene.DetonationPrep5, "T. Stage 5 detonation prep (0.25x)",
                "T. Aşama 5 patlama hazırlığı (0.25x)");
            AddMag(AnimMagScene.Explosion5, "U. Stage 5 full explosion", "U. Aşama 5 tam patlama");
            AddMag(AnimMagScene.SideBySide,
                "V. OLD (left) vs NEW (right), stages 1 / 3 / 5",
                "V. ESKİ (sol) - YENİ (sağ), aşama 1 / 3 / 5");
            AddMag(AnimMagScene.GrainsBeat, "extra: load beat - powder grains",
                "ek: yükleme vuruşu - barut taneleri");
            AddMag(AnimMagScene.IgnitionBeat, "extra: load beat - primer ignition + pocket bloom",
                "ek: yükleme vuruşu - primer ateşleme + cep açılması");
            AddMag(AnimMagScene.StrapHeatBeat, "extra: load beat - strap heat",
                "ek: yükleme vuruşu - kayış ısısı");
            AddMag(AnimMagScene.SmallBlock, "extra: small block (ember notches)",
                "ek: küçük blok (kor çentikleri)");
            AddMag(AnimMagScene.LargeBlock, "extra: large block", "ek: büyük blok");
            AddMag(AnimMagScene.Irregular, "extra: irregular shape", "ek: düzensiz şekil");
            AddMag(AnimMagScene.Stress, "extra: five packs loading together",
                "ek: beş paket birlikte yükleniyor");
            AddMag(AnimMagScene.LoadQuarter, "extra: 0.25x load", "ek: 0.25x yükleme");
            AddMag(AnimMagScene.Detonate1, "extra: stage 1 detonation", "ek: aşama 1 patlama");
            AddMag(AnimMagScene.Detonate3, "extra: stage 3 detonation", "ek: aşama 3 patlama");

            AddMagToggle("primer system", "primer sistemi",
                delegate { return PowderMagazineView.Layers.ShowPrimerSystem; },
                delegate (bool v) { PowderMagazineView.Layers.ShowPrimerSystem = v; });
            AddMagToggle("internal pockets", "iç cepler",
                delegate { return PowderMagazineView.Layers.ShowPockets; },
                delegate (bool v) { PowderMagazineView.Layers.ShowPockets = v; });
            AddMagToggle("strap underglow", "kayış altı ışığı",
                delegate { return PowderMagazineView.Layers.ShowStrapUnderglow; },
                delegate (bool v) { PowderMagazineView.Layers.ShowStrapUnderglow = v; });
            AddMagToggle("seam heat", "dikiş ısısı",
                delegate { return PowderMagazineView.Layers.ShowSeamHeat; },
                delegate (bool v) { PowderMagazineView.Layers.ShowSeamHeat = v; });
            AddMagToggle("strap tension", "kayış gerilimi",
                delegate { return PowderMagazineView.Layers.ShowStrapTension; },
                delegate (bool v) { PowderMagazineView.Layers.ShowStrapTension = v; });
            AddMagToggle("powder grains", "barut taneleri",
                delegate { return PowderMagazineView.Layers.ShowPowderGrains; },
                delegate (bool v) { PowderMagazineView.Layers.ShowPowderGrains = v; });
            AddMagToggle("soot + creases", "kurum + kıvrımlar",
                delegate { return PowderMagazineView.Layers.ShowSootMask; },
                delegate (bool v) { PowderMagazineView.Layers.ShowSootMask = v; });
            AddMagToggle("pressure pulse", "basınç nabzı",
                delegate { return PowderMagazineView.Layers.ShowPressurePulse; },
                delegate (bool v) { PowderMagazineView.Layers.ShowPressurePulse = v; });
            AddMagToggle("heat distortion", "ısı kırılması",
                delegate { return PowderMagazineView.Layers.ShowHeatDistortion; },
                delegate (bool v) { PowderMagazineView.Layers.ShowHeatDistortion = v; });
            AddMagToggle("group bounds", "grup sınırları",
                delegate { return PowderMagazineView.Layers.ShowGunpowderGroupBounds; },
                delegate (bool v) { PowderMagazineView.Layers.ShowGunpowderGroupBounds = v; });
            AddMagToggle("charge stage (dev label)", "şarj aşaması (geliştirici etiketi)",
                delegate { return PowderMagazineView.Layers.ShowChargeStage; },
                delegate (bool v) { PowderMagazineView.Layers.ShowChargeStage = v; });
            AddMagToggle("primer states (dev label)", "primer durumları (geliştirici etiketi)",
                delegate { return PowderMagazineView.Layers.ShowPrimerStates; },
                delegate (bool v) { PowderMagazineView.Layers.ShowPrimerStates = v; });
            AddMagToggle("detonation energy flow (louder)", "patlama enerji akışı (belirgin)",
                delegate { return PowderMagazineView.Layers.ShowDetonationEnergyFlow; },
                delegate (bool v) { PowderMagazineView.Layers.ShowDetonationEnergyFlow = v; });
            AddMagToggle("reward value (dev label)", "ödül değeri (geliştirici etiketi)",
                delegate { return PowderMagazineView.Layers.ShowRewardValue; },
                delegate (bool v) { PowderMagazineView.Layers.ShowRewardValue = v; });
            AddAnim("barut switch: ALL back on", "barut anahtarı: TÜMÜ geri açık", delegate
            {
                PowderMagazineView.Layers.AllOn();
                animMagazineFeatures = null;
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
            if (boardView != null && animMagazineHeld.Count > 0)
            {
                boardView.ReleaseCells(animMagazineHeld);
            }
            animMagazineHeld.Clear();
            PowderMagazineView.Layers.OnlyPhase = PowderMagazineView.LoadPhase.All;
            if (animMagazineFeatures != null)
            {
                PowderMagazineView.Layers.RestoreFeatures(animMagazineFeatures);
                animMagazineFeatures = null;
            }
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

        /// <summary>A three-cube run - the rows of the "only" scenes and the before/after.</summary>
        private static MagBlock MagRun(int x0, int y, int charges, int cardId)
        {
            var cells = new List<GridPos>
            {
                new GridPos(x0, y), new GridPos(x0 + 1, y), new GridPos(x0 + 2, y)
            };
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
                case AnimMagScene.Static3: case AnimMagScene.Load3: case AnimMagScene.Detonate3:
                case AnimMagScene.GrainsBeat: case AnimMagScene.IgnitionBeat:
                case AnimMagScene.StrapHeatBeat: case AnimMagScene.SmallBlock:
                case AnimMagScene.Irregular: case AnimMagScene.LoadQuarter:
                    return 3;
                case AnimMagScene.Static4: case AnimMagScene.Load4: case AnimMagScene.LargeBlock:
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
                case AnimMagScene.Load4: case AnimMagScene.Load5: case AnimMagScene.GrainsBeat:
                case AnimMagScene.IgnitionBeat: case AnimMagScene.StrapHeatBeat:
                case AnimMagScene.LoadQuarter: case AnimMagScene.SmallBlock:
                case AnimMagScene.LargeBlock: case AnimMagScene.Irregular:
                    return true;
                default:
                    return false;
            }
        }

        private static bool MagIsDetonation(AnimMagScene scene)
        {
            switch (scene)
            {
                case AnimMagScene.DetonationPrep5: case AnimMagScene.Explosion5:
                case AnimMagScene.Detonate1: case AnimMagScene.Detonate3:
                    return true;
                default:
                    return false;
            }
        }

        private static float MagRate(AnimMagScene scene)
        {
            switch (scene)
            {
                case AnimMagScene.LoadQuarter: case AnimMagScene.DetonationPrep5: return 0.25f;
                default: return 1f;
            }
        }

        private static PowderMagazineView.LoadPhase MagPhase(AnimMagScene scene)
        {
            switch (scene)
            {
                case AnimMagScene.GrainsBeat: return PowderMagazineView.LoadPhase.Grains;
                case AnimMagScene.IgnitionBeat: return PowderMagazineView.LoadPhase.Ignition;
                case AnimMagScene.StrapHeatBeat: return PowderMagazineView.LoadPhase.StrapHeat;
                default: return PowderMagazineView.LoadPhase.All;
            }
        }

        /// <summary>The feature an "only" scene keeps, if it is one.</summary>
        private static bool MagOnly(AnimMagScene scene, out PowderMagazineView.Feature feature)
        {
            switch (scene)
            {
                case AnimMagScene.PrimersOnly: feature = PowderMagazineView.Feature.Primers; return true;
                case AnimMagScene.PocketsOnly: feature = PowderMagazineView.Feature.Pockets; return true;
                case AnimMagScene.UnderglowOnly: feature = PowderMagazineView.Feature.Underglow; return true;
                case AnimMagScene.SeamHeatOnly: feature = PowderMagazineView.Feature.SeamHeat; return true;
                case AnimMagScene.PressurePulseOnly:
                    feature = PowderMagazineView.Feature.PressurePulse;
                    return true;
                case AnimMagScene.HeatDistortionOnly:
                    feature = PowderMagazineView.Feature.HeatDistortion;
                    return true;
                default:
                    feature = PowderMagazineView.Feature.Primers;
                    return false;
            }
        }

        private static bool MagIsTriple(AnimMagScene scene)
        {
            return scene == AnimMagScene.PrimersOnly || scene == AnimMagScene.PocketsOnly
                || scene == AnimMagScene.UnderglowOnly || scene == AnimMagScene.SeamHeatOnly;
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

            PowderMagazineView.Feature only;
            if (MagOnly(scene, out only))
            {
                if (animMagazineFeatures == null)
                {
                    animMagazineFeatures = PowderMagazineView.Layers.CaptureFeatures();
                }
                PowderMagazineView.Layers.Only(only);
            }
            if (scene == AnimMagScene.SideBySide || MagIsTriple(scene))
            {
                MagTriple(w, h, scene == AnimMagScene.SideBySide);
                animLastLabel = scene == AnimMagScene.SideBySide
                    ? Loc.Pick("OLD - jitter and crisp seams (left) | NEW - controlled accumulation (right): "
                        + "stages 1, 3, 5",
                        "ESKİ - titreme ve keskin dikişler (sol) | YENİ - kontrollü birikim (sağ): aşama 1, 3, 5")
                    : Loc.Pick(only + " only: stages 1, 3, 5", "yalnız " + only + ": aşama 1, 3, 5");
                if (AnimLabOpen)
                {
                    RedrawAnimationLab();
                }
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
            else if (scene == AnimMagScene.PressurePulseOnly)
            {
                // The rare pulse, asked for again and again so it can be watched on its own.
                yield return new WaitForSeconds(0.4f);
                for (int i = 0; i < 6; i++)
                {
                    magazine.ForceIdle();
                    animLastLabel = Loc.Pick("pressure pulse " + (i + 1) + " / 6",
                        "basınç nabzı " + (i + 1) + " / 6");
                    if (AnimLabOpen)
                    {
                        RedrawAnimationLab();
                    }
                    yield return new WaitForSeconds(1.3f);
                }
            }
            else if (scene == AnimMagScene.Idle5)
            {
                // One event at once so there is something to see, then the pack's own rhythm.
                yield return new WaitForSeconds(0.4f);
                magazine.ForceIdle();
                animLastLabel = Loc.Pick("magazine: stage 5 idle - pulse every 2.5-4s, smoke every 4-7s",
                    "şarjör: aşama 5 bekleme - nabız 2.5-4 sn, duman 4-7 sn");
            }
            else if (scene == AnimMagScene.HeatDistortionOnly)
            {
                animLastLabel = Loc.Pick("heat distortion only: inside the pockets, 0.8 px, slow",
                    "yalnız ısı kırılması: ceplerin içinde, 0.8 px, yavaş");
            }
            else if (scene == AnimMagScene.MaxLock)
            {
                yield return new WaitForSeconds(0.6f);
                magazine.ForceLock();
                animLastLabel = Loc.Pick("magazine: MAX CHARGE LOCK", "şarjör: MAKSİMUM ŞARJ KİLİDİ");
            }
            else if (MagIsDetonation(scene))
            {
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

        /// <summary>
        /// Three packs at stages 1, 3 and 5, one row each. With <paramref name="beforeAfter"/> a
        /// second column on the left draws the same three the PREVIOUS pass's way, so the only
        /// difference between the two columns is the language; otherwise the three sit in the
        /// middle, for the "only" scenes.
        /// </summary>
        private void MagTriple(int w, int h, bool beforeAfter)
        {
            var oldBlocks = new List<MagBlock>();
            var newBlocks = new List<MagBlock>();
            int[] stages = { 1, 3, 5 };
            for (int s = 0; s < stages.Length; s++)
            {
                int y = h - 2 - s * 2;
                if (beforeAfter)
                {
                    oldBlocks.Add(MagRun(0, y, stages[s], 9201 + s));
                    newBlocks.Add(MagRun(w - 3, y, stages[s], 9301 + s));
                }
                else
                {
                    newBlocks.Add(MagRun(w / 2 - 1, y, stages[s], 9301 + s));
                }
            }
            var all = new List<MagBlock>(oldBlocks);
            all.AddRange(newBlocks);
            MagBoard(all);
            var oldIds = new List<int>();
            for (int i = 0; i < oldBlocks.Count; i++)
            {
                oldIds.Add(oldBlocks[i].CardId);
            }
            magazine.SetOldLook(oldIds);
            magazine.PlaybackRate = 1f;
            magazine.Show(MagReport(all, false), false);
        }
    }
}
