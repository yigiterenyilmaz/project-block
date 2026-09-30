// PURPOSE: "Antimadde" in the ANIMATION LAB (F3) - the annihilation event's own section.
//
// Every scene puts up a 7x7 board of the lab's own with the element where the scene wants it (and
// ordinary cubes round it where the shockwave's reflections are the point), empties the targets the
// way the rules do (FORCED - gold and obsidian go too), repaints, and hands AntimatterBlastView the
// same Request the game builds: the cells, the faces the board last showed (AntimatterTarget, the
// game's own), and the points at the joker's OWN per-cube price and the round's score scale. It is
// played through Prepare/Begin - the game's calls - so a retimed beat shows here for free.
//
// The "only" scenes switch every effect layer off and the one they show back on BEFORE the event is
// planned, because a layer that is off is a layer that is never rented. The cleanup scene plays the
// sweep through PlayDeferredSweep, the very routine the game defers a caused sweep with. Nothing
// here touches the round's Core state.

using System.Collections;
using System.Collections.Generic;
using ProjectBlock.Core;
using UnityEngine;

namespace ProjectBlock.View
{
    partial class GameUiController
    {
        private Coroutine animAntimatterRoutine;
        private uint animAntimatterSeed = 1;

        /// <summary>The lab board's cells, in the order scenes take them: eight spread by hand, then
        /// the rest of the 7x7 in a fixed scattered order.</summary>
        private static readonly GridPos[] AnimAmCells = BuildAnimAmCells();

        private static GridPos[] BuildAnimAmCells()
        {
            var first = new List<GridPos>
            {
                new GridPos(3, 3), new GridPos(5, 5), new GridPos(1, 1), new GridPos(5, 1),
                new GridPos(1, 5), new GridPos(3, 0), new GridPos(0, 3), new GridPos(6, 3)
            };
            var rest = new List<KeyValuePair<float, GridPos>>();
            for (int y = 0; y < 7; y++)
            {
                for (int x = 0; x < 7; x++)
                {
                    var p = new GridPos(x, y);
                    if (!first.Contains(p))
                    {
                        float h = Mathf.Repeat(Mathf.Sin(x * 12.9898f + y * 78.233f) * 43758.5453f, 1f);
                        rest.Add(new KeyValuePair<float, GridPos>(h, p));
                    }
                }
            }
            rest.Sort((a, b) => a.Key.CompareTo(b.Key));
            foreach (KeyValuePair<float, GridPos> entry in rest)
            {
                first.Add(entry.Value);
            }
            return first.ToArray();
        }

        private void AddAntimatterLab()
        {
            AddAnimSub("jokers", "antimadde", "antimadde - annihilation event", "antimadde - yok oluş olayı");

            // --- LOCAL ---
            AddAnim("antimadde: 1 single target idle (held, not begun)", "antimadde: 1 tek hedef bekler (tutulur, başlamaz)",
                delegate { AnimAmFull(); AnimAm(CubeKind.Gold, 1, false, "single target idle", "tek hedef bekler", true); });
            AddAnim("antimadde: 2 antimatter ghost birth (held after birth)", "antimadde: 2 antimadde hayaleti doğar (doğumdan sonra tutulur)",
                delegate
                {
                    AnimAmOnly();
                    AntimatterBlastView.Layers.Ghosts = true;
                    AntimatterBlastView.Layers.Notice = true;
                    AntimatterBlastView.Style.HoldAt = AntimatterBlastView.Style.GhostStart + AntimatterBlastView.Style.GhostDuration + 0.06f;
                    AnimAm(CubeKind.Gold, 1, false, "ghost birth", "hayalet doğumu");
                });
            AddAnim("antimadde: 3 matter / antimatter side by side (held apart)", "antimadde: 3 madde / antimadde yan yana (ayrık tutulur)",
                delegate
                {
                    AnimAmOnly();
                    AntimatterBlastView.Layers.Ghosts = true;
                    AntimatterBlastView.Style.GhostApart = 0.6f;
                    AntimatterBlastView.Style.HoldAt = 0.3f;
                    AnimAm(CubeKind.Gold, 1, false, "matter / antimatter side by side", "madde / antimadde yan yana");
                });
            AddAnimAmOnly("4 ghost collision", "4 hayalet çarpışması", CubeKind.Gold, 1,
                delegate { AntimatterBlastView.Layers.Ghosts = true; AntimatterBlastView.Layers.Contact = true; });
            AddAnimAmOnly("5 local core only", "5 yalnızca yerel çekirdek", CubeKind.Gold, 1,
                delegate { AntimatterBlastView.Layers.Cores = true; });
            AddAnimAmOnly("6 inward ring only", "6 yalnızca içe halka", CubeKind.Gold, 1,
                delegate { AntimatterBlastView.Layers.InwardRings = true; });
            AddAnimAmOnly("7 UV pull only", "7 yalnızca UV çekmesi", CubeKind.Gold, 1,
                delegate { AntimatterBlastView.Layers.UvPull = true; });
            AddAnimAmOnly("8 silent implosion only", "8 yalnızca sessiz çöküş", CubeKind.Gold, 1,
                delegate { AntimatterBlastView.Layers.Implosion = true; });
            AddAnimAmOnly("9 peak core only", "9 yalnızca zirve çekirdeği", CubeKind.Gold, 1,
                delegate { AntimatterBlastView.Layers.PeakCores = true; });
            AddAnimAmOnly("10 beams only", "10 yalnızca hüzmeler", CubeKind.Gold, 1,
                delegate { AntimatterBlastView.Layers.Beams = true; });
            AddAnimAmOnly("11 local shockwave only", "11 yalnızca yerel şok dalgası", CubeKind.Gold, 1,
                delegate { AntimatterBlastView.Layers.LocalWaves = true; });
            AddAnimAmOnly("12 negative afterimage only", "12 yalnızca negatif iz", CubeKind.Gold, 1,
                delegate { AntimatterBlastView.Layers.Afterimages = true; });
            AddAnimAmOnly("13 spiral return only", "13 yalnızca spiral dönüş", CubeKind.Gold, 1,
                delegate { AntimatterBlastView.Layers.Particles = true; });

            // --- FULL ---
            AddAnimAmFull("14 one target, full", "14 tek hedef, tam", CubeKind.Gold, 1, false);
            AddAnimAmFull("15 two targets", "15 iki hedef", CubeKind.Gold, 2, true);
            AddAnimAmFull("16 four targets", "16 dört hedef", CubeKind.Gold, 4, true);
            AddAnimAmFull("17 eight targets", "17 sekiz hedef", CubeKind.Gold, 8, true);
            AddAnimAmFull("18 sixteen targets", "18 on altı hedef", CubeKind.Water, 16, true);
            AddAnimAmFull("19 thirty targets (stress)", "19 otuz hedef (stres)", CubeKind.Water, 30, true);

            // --- ELEMENTS ---
            AddAnimAmFull("20 fire annihilation", "20 ateş yok oluşu", CubeKind.Fire, 6, true);
            AddAnimAmFull("21 water annihilation", "21 su yok oluşu", CubeKind.Water, 6, true);
            AddAnimAmFull("22 gold annihilation", "22 altın yok oluşu", CubeKind.Gold, 6, true);
            AddAnimAmFull("23 obsidian annihilation", "23 obsidyen yok oluşu", CubeKind.Obsidian, 6, true);
            AddAnim("antimadde: 24 mixed visual test (fire, water, gold, obsidian in turn)",
                "antimadde: 24 karışık görsel test (sırayla ateş, su, altın, obsidyen)",
                delegate
                {
                    AnimAmFull();
                    StopAnimAntimatter();
                    animAntimatterRoutine = StartCoroutine(AnimAmMixed());
                });

            // --- GLOBAL ---
            AddAnimAmOnly("25 filaments only", "25 yalnızca iplikler", CubeKind.Gold, 8,
                delegate { AntimatterBlastView.Layers.Filaments = true; });
            AddAnimAmOnly("26 global centroid lens", "26 genel merkez merceği", CubeKind.Gold, 8,
                delegate { AntimatterBlastView.Layers.Lens = true; AntimatterBlastView.Layers.ShowCentroid = true; });
            AddAnimAmOnly("27 global shockwave", "27 genel şok dalgası", CubeKind.Gold, 8,
                delegate { AntimatterBlastView.Layers.Shockwave = true; AntimatterBlastView.Layers.Reflections = true; }, true);
            AddAnimAmOnly("28 energy crown", "28 enerji tacı", CubeKind.Gold, 8,
                delegate { AntimatterBlastView.Layers.Crown = true; AntimatterBlastView.Layers.CentralCore = true; });
            AddAnimAmOnly("29 light column", "29 ışık sütunu", CubeKind.Gold, 8,
                delegate { AntimatterBlastView.Layers.Column = true; });
            AddAnimAmOnly("30 board impulse", "30 tahta itkisi", CubeKind.Gold, 8,
                delegate { AntimatterBlastView.Layers.BoardImpulse = true; }, true);
            AddAnimAmOnly("31 background dimming", "31 arka plan kararması", CubeKind.Gold, 8,
                delegate { AntimatterBlastView.Layers.BackgroundDim = true; AntimatterBlastView.Layers.BoardDim = true; }, true);
            AddAnimAmOnly("32 screen chromatic edge", "32 ekran kenarı renk sapması", CubeKind.Gold, 8,
                delegate { AntimatterBlastView.Layers.EdgeFringe = true; });

            // --- REWARD ---
            AddAnimAmFull("33 low-count score (per-cube shares + total)", "33 az hedef puanı (küp başı + toplam)", CubeKind.Gold, 2, false);
            AddAnimAmFull("34 high-count aggregate score", "34 çok hedef toplam puan", CubeKind.Gold, 16, true);
            AddAnimAmOnly("35 score travel only", "35 yalnızca puan yolculuğu", CubeKind.Gold, 8,
                delegate { AntimatterBlastView.Layers.Score = true; AntimatterBlastView.Layers.ShowScoreAnchor = true; });

            // --- SEQUENCING ---
            AddAnim("antimadde: 36 annihilation -> cleanup (the board empties, the sweep waits)",
                "antimadde: 36 yok oluş -> temizlik (tahta boşalır, temizlik bekler)",
                delegate
                {
                    AnimAmFull();
                    AnimAm(CubeKind.Gold, 6, false, "annihilation -> cleanup", "yok oluş -> temizlik");
                    RoundEngine round = session != null ? session.CurrentRound : null;
                    StopAnimAntimatter();
                    animAntimatterRoutine = StartCoroutine(PlayDeferredSweep(round, AntimatterBlastView.Style.CleanupDelay));
                });
            AddAnimAmFull("37 annihilation, no cleanup", "37 yok oluş, temizlik yok", CubeKind.Gold, 6, true);

            // --- SPEED ---
            AddAnim("antimadde: 38 eight gold at 1x", "antimadde: 38 sekiz altın 1x",
                delegate { AnimAmFull(); Time.timeScale = 1f; AnimAm(CubeKind.Gold, 8, true, "1x", "1x"); });
            AddAnim("antimadde: 39 eight gold at 0.5x", "antimadde: 39 sekiz altın 0.5x",
                delegate { AnimAmFull(); Time.timeScale = 0.5f; AnimAm(CubeKind.Gold, 8, true, "0.5x", "0.5x"); });
            AddAnim("antimadde: 40 eight gold at 0.25x", "antimadde: 40 sekiz altın 0.25x",
                delegate { AnimAmFull(); Time.timeScale = 0.25f; AnimAm(CubeKind.Gold, 8, true, "0.25x", "0.25x"); });

            // --- QUALITY ---
            AddAnimAmQuality("41 quality HIGH", "41 kalite YÜKSEK", AntimatterBlastView.Quality.High);
            AddAnimAmQuality("42 quality MEDIUM", "42 kalite ORTA", AntimatterBlastView.Quality.Medium);
            AddAnimAmQuality("43 quality LOW", "43 kalite DÜŞÜK", AntimatterBlastView.Quality.Low);

            AddAnim("antimadde: every layer back on", "antimadde: tüm katmanlar açık",
                delegate
                {
                    AntimatterBlastView.Layers.AllOn();
                    animLastLabel = Loc.Pick("antimadde: every layer back on", "antimadde: tüm katmanlar açık");
                    if (AnimLabOpen) { RedrawAnimationLab(); }
                });

            // --- DEBUG ---
            AddAnimAmToggle("targets (matter colour)", "hedefler (madde rengi)", () => AntimatterBlastView.Layers.ShowTargets, v => AntimatterBlastView.Layers.ShowTargets = v);
            AddAnimAmToggle("antimatter ghosts (violet)", "antimadde hayaletleri (mor)", () => AntimatterBlastView.Layers.ShowGhosts, v => AntimatterBlastView.Layers.ShowGhosts = v);
            AddAnimAmToggle("filament graph", "iplik grafiği", () => AntimatterBlastView.Layers.ShowFilaments, v => AntimatterBlastView.Layers.ShowFilaments = v);
            AddAnimAmToggle("cores", "çekirdekler", () => AntimatterBlastView.Layers.ShowCores, v => AntimatterBlastView.Layers.ShowCores = v);
            AddAnimAmToggle("inward rings", "içe halkalar", () => AntimatterBlastView.Layers.ShowInwardRings, v => AntimatterBlastView.Layers.ShowInwardRings = v);
            AddAnimAmToggle("UV pull (orange = pulled edge)", "UV çekmesi (turuncu = çekilen kenar)", () => AntimatterBlastView.Layers.ShowUvPull, v => AntimatterBlastView.Layers.ShowUvPull = v);
            AddAnimAmToggle("implosion scale (white)", "çöküş ölçeği (beyaz)", () => AntimatterBlastView.Layers.ShowImplosionScale, v => AntimatterBlastView.Layers.ShowImplosionScale = v);
            AddAnimAmToggle("hero beam origins (cyan)", "kahraman hüzme kökleri (camgöbeği)", () => AntimatterBlastView.Layers.ShowHeroBeamOrigins, v => AntimatterBlastView.Layers.ShowHeroBeamOrigins = v);
            AddAnimAmToggle("beam directions", "hüzme yönleri", () => AntimatterBlastView.Layers.ShowBeamDirections, v => AntimatterBlastView.Layers.ShowBeamDirections = v);
            AddAnimAmToggle("global centroid (yellow)", "genel merkez (sarı)", () => AntimatterBlastView.Layers.ShowCentroid, v => AntimatterBlastView.Layers.ShowCentroid = v);
            AddAnimAmToggle("global lens reach", "genel mercek erimi", () => AntimatterBlastView.Layers.ShowLens, v => AntimatterBlastView.Layers.ShowLens = v);
            AddAnimAmToggle("shockwave reach and front", "şok dalgası erimi ve cephesi", () => AntimatterBlastView.Layers.ShowShockwave, v => AntimatterBlastView.Layers.ShowShockwave = v);
            AddAnimAmToggle("negative afterimages", "negatif izler", () => AntimatterBlastView.Layers.ShowAfterimages, v => AntimatterBlastView.Layers.ShowAfterimages = v);
            AddAnimAmToggle("spiral paths", "spiral yollar", () => AntimatterBlastView.Layers.ShowSpiralPaths, v => AntimatterBlastView.Layers.ShowSpiralPaths = v);
            AddAnimAmToggle("score anchor and target", "puan çıkışı ve hedefi", () => AntimatterBlastView.Layers.ShowScoreAnchor, v => AntimatterBlastView.Layers.ShowScoreAnchor = v);
            AddAnimAmToggle("particle / beam budget", "parçacık / hüzme bütçesi", () => AntimatterBlastView.Layers.ShowBudget, v => AntimatterBlastView.Layers.ShowBudget = v);
            AddAnim("antimadde debug: all off", "antimadde hata ayıklama: hepsi kapalı",
                delegate
                {
                    AntimatterBlastView.Layers.DebugOff();
                    animLastLabel = Loc.Pick("antimadde debug: all off", "antimadde hata ayıklama: hepsi kapalı");
                    if (AnimLabOpen) { RedrawAnimationLab(); }
                });
        }

        private void AddAnimAmOnly(string en, string tr, CubeKind kind, int count, System.Action switchOn,
            bool bystanders = false)
        {
            AddAnim("antimadde: " + en, "antimadde: " + tr,
                delegate
                {
                    AnimAmOnly();
                    switchOn();
                    AnimAm(kind, count, bystanders, en, tr);
                });
        }

        private void AddAnimAmFull(string en, string tr, CubeKind kind, int count, bool bystanders)
        {
            AddAnim("antimadde: " + en, "antimadde: " + tr,
                delegate
                {
                    AnimAmFull();
                    AnimAm(kind, count, bystanders, en, tr);
                });
        }

        private void AddAnimAmQuality(string en, string tr, AntimatterBlastView.Quality quality)
        {
            AddAnim("antimadde: " + en + " (12 targets)", "antimadde: " + tr + " (12 hedef)",
                delegate
                {
                    AnimAmFull();
                    AntimatterBlastView.Style.Level = quality;
                    AnimAm(CubeKind.Gold, 12, true, en, tr);
                });
        }

        private void AddAnimAmToggle(string en, string tr, System.Func<bool> read, System.Action<bool> write)
        {
            AddAnim("antimadde debug: " + en, "antimadde hata ayıklama: " + tr,
                delegate
                {
                    write(!read());
                    animLastLabel = Loc.Pick("antimadde debug " + en + ": ", "antimadde hata ayıklama " + tr + ": ")
                        + OnOff(read());
                    if (AnimLabOpen) { RedrawAnimationLab(); }
                });
        }

        /// <summary>Every effect layer off; the scene switches on the ones it shows. The lab's
        /// holds are cleared too, so an "only" scene never inherits a freeze.</summary>
        private void AnimAmOnly()
        {
            AntimatterBlastView.Layers.AllOff();
            AntimatterBlastView.Style.HoldAt = -1f;
            AntimatterBlastView.Style.GhostApart = 0f;
        }

        private void AnimAmFull()
        {
            AntimatterBlastView.Layers.AllOn();
            AntimatterBlastView.Style.HoldAt = -1f;
            AntimatterBlastView.Style.GhostApart = 0f;
        }

        /// <summary>
        /// One scene: a board of the lab's own with <paramref name="count"/> cubes of
        /// <paramref name="kind"/> (and ordinary cubes round them when the reflections matter),
        /// emptied the way the rules empty it, then the game's own Request played through
        /// Prepare/Begin.
        /// </summary>
        private void AnimAm(CubeKind kind, int count, bool bystanders, string english, string turkish,
            bool prepareOnly = false)
        {
            RoundEngine round = session != null ? session.CurrentRound : null;
            if (round == null || boardView == null)
            {
                return;
            }
            StopAnimAntimatter();
            EnsureAntimatter();
            antimatter.Stop();
            antimatter.Forget();
            List<int> cards = AnimBossCards();
            var board = new GameBoard(7, 7);
            var targets = new List<GridPos>();
            for (int i = 0; i < count && i < AnimAmCells.Length; i++)
            {
                targets.Add(AnimAmCells[i]);
                board.SetCubeAt(AnimAmCells[i], new Cube(kind, cards[0]));
            }
            if (bystanders)
            {
                // The cubes that stay: ordinary blocks and one stone, which the shockwave only
                // lights as it passes.
                for (int y = 0; y < 7; y++)
                {
                    for (int x = 0; x < 7; x++)
                    {
                        var p = new GridPos(x, y);
                        if (board.GetCube(p).HasValue || (x * 3 + y * 5) % 3 != 0)
                        {
                            continue;
                        }
                        CubeKind other = kind != CubeKind.Obsidian && (x + y) % 5 == 0 ? CubeKind.Obsidian : CubeKind.Normal;
                        board.SetCubeAt(p, new Cube(other, cards[(x + y) % cards.Count]));
                    }
                }
            }
            boardView.Rebuild(board, MainBoardWorldSize, MainBoardCenter);
            boardView.Refresh();
            var cubes = new List<Cube>();
            foreach (GridPos p in targets)
            {
                cubes.Add(board.GetCube(p).Value);
            }
            // Emptied the way the rules empty it: FORCED, so gold and obsidian go too.
            foreach (GridPos p in targets)
            {
                board.DestroyCubeForced(p);
            }
            boardView.Refresh();

            AntimaddeJoker joker = FindAntimatterJoker() ?? new AntimaddeJoker();
            int scale = session.Config.Scoring.ScoreScale;
            var request = new AntimatterBlastView.Request
            {
                Kind = kind,
                PointsPerCube = joker.BonusPerCube * scale,
                Points = joker.BonusPerCube * scale * targets.Count,
                Seed = animAntimatterSeed++ * 2654435761u
            };
            for (int i = 0; i < targets.Count; i++)
            {
                request.Targets.Add(AntimatterTarget(targets[i], cubes[i]));
            }
            var key = new object();
            antimatter.Prepare(key, request, prepareOnly);
            if (!prepareOnly)
            {
                antimatter.Begin(key, request);
            }
            animLastLabel = Loc.Pick(
                "antimadde: " + english + " (" + targets.Count + " " + ViewUtil.KindLabel(kind) + ", +" + request.Points + ")",
                "antimadde: " + turkish + " (" + targets.Count + " " + ViewUtil.KindLabel(kind) + ", +" + request.Points + ")");
            if (AnimLabOpen)
            {
                RedrawAnimationLab();
            }
        }

        private IEnumerator AnimAmMixed()
        {
            CubeKind[] kinds = { CubeKind.Fire, CubeKind.Water, CubeKind.Gold, CubeKind.Obsidian };
            string[] en = { "fire", "water", "gold", "obsidian" };
            string[] tr = { "ateş", "su", "altın", "obsidyen" };
            for (int i = 0; i < kinds.Length; i++)
            {
                // AnimAm stops the lab's running routine - which is this one - so it is hidden from
                // it for the call.
                Coroutine self = animAntimatterRoutine;
                animAntimatterRoutine = null;
                AnimAm(kinds[i], 3, true, "mixed " + (i + 1) + "/4 " + en[i], "karışık " + (i + 1) + "/4 " + tr[i]);
                animAntimatterRoutine = self;
                yield return new WaitForSeconds(1.7f);
            }
            animAntimatterRoutine = null;
        }

        private AntimaddeJoker FindAntimatterJoker()
        {
            if (session == null || session.Jokers == null)
            {
                return null;
            }
            IReadOnlyList<Joker> owned = session.Jokers.Jokers;
            for (int i = 0; i < owned.Count; i++)
            {
                var found = owned[i] as AntimaddeJoker;
                if (found != null)
                {
                    return found;
                }
            }
            return null;
        }

        private void StopAnimAntimatter()
        {
            if (animAntimatterRoutine != null)
            {
                StopCoroutine(animAntimatterRoutine);
                animAntimatterRoutine = null;
            }
        }
    }
}
