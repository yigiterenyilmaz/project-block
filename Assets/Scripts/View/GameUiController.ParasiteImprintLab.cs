// PURPOSE: The animation lab's "PARAZİT / HOSTED JOKER IMPRINT" section - every scene the imprint
// needs to be judged by (ParasiteHostView.Imprint), on a board of the lab's own.
//
// The rule the whole lab follows holds here: the scenes drive the REAL host view through the same
// seams the game drives it through (Sync, SetRider, PlayAttach, PlaySeverance, the board's own
// refusal log) and fabricate only the ARGUMENTS - which cube is the host, what it is made of, which
// joker rides it, and where the icon sets off from. Two scenes fabricate a little more, and say so:
// the colour tests draw a block of a colour no card happens to be (Host.DrawBase), and the
// side-by-side draws the OLD sticker look beside the new one, because the question it asks is "on
// which of these is the joker really inside the parasite?" and that needs both on screen.
//
// Like every lab entry each scene HOLDS what it ends on; the switches apply to what is standing,
// and "all imprint switches back on" is the reset. The attach at 0.5x and 0.25x slows only the
// attach itself (ImprintLayers.AttachRate), never the game.

using System.Collections;
using System.Collections.Generic;
using ProjectBlock.Core;
using UnityEngine;

namespace ProjectBlock.View
{
    partial class GameUiController
    {
        private enum AnimImprintScene
        {
            RawMembrane,
            RawIconOnTop,
            Final,
            AttachFull,
            ApproachOnly,
            DimpleOnly,
            SinkOnly,
            GrabOnly,
            SealOnly,
            FinalIdle,
            Struggle,
            Wave,
            Destroyed,
            Resist,
            Green,
            Pink,
            Blue,
            Obsidian,
            Gold,
            VeryLight,
            VeryDark,
            Midas,
            Yangin,
            Buzluk,
            Hazine,
            Complex,
            Simple,
            Grid,
            Attach1x,
            AttachHalf,
            AttachQuarter,
            SideBySide
        }

        private Coroutine animImprint;

        private AnimImprintScene animImprintLast = AnimImprintScene.AttachFull;

        /// <summary>The ten icons of the readability grid - chosen to span warm, cold, dark,
        /// busy and plain, and one (kara_delik) that is nearly the membrane's own colour.</summary>
        private static readonly string[] AnimImprintGrid =
        {
            "midas", "yangin", "buzluk", "hazine", "iade",
            "robot_supurge", "kara_delik", "kredi_karti", "tutustur", "simya"
        };

        private void AddParasiteImprintAnims()
        {
            AddAnimSub("jokers", "parazit_ikon", "parazit - hosted joker imprint",
                "parazit - konaktaki joker izi");
            AddImprint(AnimImprintScene.RawMembrane, "1. Raw host membrane, no icon",
                "1. İkonsuz ham konak zarı");
            AddImprint(AnimImprintScene.RawIconOnTop,
                "2. Raw icon ON TOP - the old sticker, comparison only",
                "2. ÜSTE konmuş ham ikon - eski etiket, yalnız karşılaştırma");
            AddImprint(AnimImprintScene.Final, "3. Final embedded icon", "3. Son hâli: gömülü ikon");
            AddImprint(AnimImprintScene.AttachFull, "4. Attach sequence, full",
                "4. Bağlanma dizisi, tam");
            AddImprint(AnimImprintScene.ApproachOnly, "5. Icon approach only",
                "5. Yalnız ikonun yaklaşması");
            AddImprint(AnimImprintScene.DimpleOnly, "6. Membrane dimple only",
                "6. Yalnız zarın çukurlaşması");
            AddImprint(AnimImprintScene.SinkOnly, "7. Icon sink only", "7. Yalnız ikonun batması");
            AddImprint(AnimImprintScene.GrabOnly, "8. Fiber grab only",
                "8. Yalnız liflerin kavraması");
            AddImprint(AnimImprintScene.SealOnly, "9. Membrane seal only",
                "9. Yalnız zarın mühürlemesi");
            AddImprint(AnimImprintScene.FinalIdle, "10. Final idle", "10. Son hâl, bekleme");
            AddImprint(AnimImprintScene.Struggle,
                "11. Host escape-struggle + icon response",
                "11. Konağın kurtulma çabası + ikonun tepkisi");
            AddImprint(AnimImprintScene.Wave, "12. Rare contraction wave",
                "12. Seyrek kasılma dalgası");
            AddImprint(AnimImprintScene.Destroyed, "13. Host destroyed + icon lost",
                "13. Konak kırılır + ikon kaybolur");
            AddImprint(AnimImprintScene.Resist, "14. Protected / resist response",
                "14. Korunma / direnme tepkisi");
            AddImprint(AnimImprintScene.Green, "15. Bright green cube + icon",
                "15. Parlak yeşil küp + ikon");
            AddImprint(AnimImprintScene.Pink, "16. Pink cube + icon", "16. Pembe küp + ikon");
            AddImprint(AnimImprintScene.Blue, "17. Blue cube + icon", "17. Mavi küp + ikon");
            AddImprint(AnimImprintScene.Obsidian, "18. Dark obsidian cube + icon",
                "18. Koyu obsidyen küp + ikon");
            AddImprint(AnimImprintScene.Gold, "19. Gold-like bright cube + icon",
                "19. Altın gibi parlak küp + ikon");
            AddImprint(AnimImprintScene.VeryLight, "20. Very light cube", "20. Çok açık küp");
            AddImprint(AnimImprintScene.VeryDark, "21. Very dark cube", "21. Çok koyu küp");
            AddImprint(AnimImprintScene.Midas, "22. Midas icon", "22. Midas ikonu");
            AddImprint(AnimImprintScene.Yangin, "23. Yangın icon", "23. Yangın ikonu");
            AddImprint(AnimImprintScene.Buzluk, "24. Buzluk icon", "24. Buzluk ikonu");
            AddImprint(AnimImprintScene.Hazine, "25. Hazine icon", "25. Hazine ikonu");
            AddImprint(AnimImprintScene.Complex, "26. Complex icon (Robot Süpürge)",
                "26. Karmaşık ikon (Robot Süpürge)");
            AddImprint(AnimImprintScene.Simple, "27. Simple icon (İade)", "27. Sade ikon (İade)");
            AddImprint(AnimImprintScene.Grid, "28. 10-icon readability grid",
                "28. 10 ikonlu okunurluk ızgarası");
            AddImprint(AnimImprintScene.Attach1x, "29. Attach at 1x", "29. Bağlanma 1x");
            AddImprint(AnimImprintScene.AttachHalf, "30. Attach at 0.5x", "30. Bağlanma 0.5x");
            AddImprint(AnimImprintScene.AttachQuarter, "31. Attach at 0.25x",
                "31. Bağlanma 0.25x");
            AddImprint(AnimImprintScene.SideBySide,
                "ACCEPTANCE: old sticker (left) vs embedded imprint (right)",
                "KABUL: eski etiket (sol) - gömülü iz (sağ)");
            AddAnim("İkon: REPLAY the last attach scene, keeping the switches",
                "ikon: son bağlanma sahnesini TEKRARLA, anahtarlar kalsın",
                delegate { AnimImprint(animImprintLast, false); });

            AddImprintToggle("ShowEmbeddedIcon", "gömülü ikon",
                delegate { return ParasiteHostView.ImprintLayers.ShowEmbeddedIcon; },
                delegate (bool v) { ParasiteHostView.ImprintLayers.ShowEmbeddedIcon = v; });
            AddImprintToggle("ShowOriginalIcon (beside the host)", "orijinal ikon (konağın yanında)",
                delegate { return ParasiteHostView.ImprintLayers.ShowOriginalIcon; },
                delegate (bool v) { ParasiteHostView.ImprintLayers.ShowOriginalIcon = v; });
            AddImprintToggle("ShowMembraneFrontMask (the veil over it)", "zarın ön maskesi (üstündeki tül)",
                delegate { return ParasiteHostView.ImprintLayers.ShowMembraneFrontMask; },
                delegate (bool v) { ParasiteHostView.ImprintLayers.ShowMembraneFrontMask = v; });
            AddImprintToggle("ShowMembraneBackMask (its bed in the film)", "zarın arka maskesi (zardaki yatağı)",
                delegate { return ParasiteHostView.ImprintLayers.ShowMembraneBackMask; },
                delegate (bool v) { ParasiteHostView.ImprintLayers.ShowMembraneBackMask = v; });
            AddImprintToggle("ShowFiberBack", "arkadaki lifler",
                delegate { return ParasiteHostView.ImprintLayers.ShowFiberBack; },
                delegate (bool v) { ParasiteHostView.ImprintLayers.ShowFiberBack = v; });
            AddImprintToggle("ShowFiberFront", "öndeki lifler",
                delegate { return ParasiteHostView.ImprintLayers.ShowFiberFront; },
                delegate (bool v) { ParasiteHostView.ImprintLayers.ShowFiberFront = v; });
            AddImprintToggle("ShowIconUVDistortion", "ikonun UV bükülmesi",
                delegate { return ParasiteHostView.ImprintLayers.ShowIconUVDistortion; },
                delegate (bool v) { ParasiteHostView.ImprintLayers.ShowIconUVDistortion = v; });
            AddImprintToggle("ShowIconOcclusion (the edges eaten)", "ikonun örtülmesi (yenen kenarlar)",
                delegate { return ParasiteHostView.ImprintLayers.ShowIconOcclusion; },
                delegate (bool v) { ParasiteHostView.ImprintLayers.ShowIconOcclusion = v; });
            AddImprintToggle("ShowIconContrastCompensation", "ikonun kontrast telafisi",
                delegate { return ParasiteHostView.ImprintLayers.ShowIconContrastCompensation; },
                delegate (bool v) { ParasiteHostView.ImprintLayers.ShowIconContrastCompensation = v; });
            AddImprintToggle("ShowIconColorRetention (off = raw colours)", "ikonun renk tutumu (kapalı = ham renk)",
                delegate { return ParasiteHostView.ImprintLayers.ShowIconColorRetention; },
                delegate (bool v) { ParasiteHostView.ImprintLayers.ShowIconColorRetention = v; });
            AddImprintToggle("ShowIconBounds", "ikon sınırları",
                delegate { return ParasiteHostView.ImprintLayers.ShowIconBounds; },
                delegate (bool v) { ParasiteHostView.ImprintLayers.ShowIconBounds = v; });
            AddImprintToggle("ShowAttachPoints (grips green/orange, roots yellow)", "tutunma noktaları (tutuş yeşil/turuncu, kök sarı)",
                delegate { return ParasiteHostView.ImprintLayers.ShowAttachPoints; },
                delegate (bool v) { ParasiteHostView.ImprintLayers.ShowAttachPoints = v; });
            AddImprintToggle("ShowMembraneDimple", "zarın çukuru",
                delegate { return ParasiteHostView.ImprintLayers.ShowMembraneDimple; },
                delegate (bool v) { ParasiteHostView.ImprintLayers.ShowMembraneDimple = v; });
            AddImprintToggle("ShowPigmentDrain", "pigment emme bölgesi",
                delegate { return ParasiteHostView.ImprintLayers.ShowPigmentDrain; },
                delegate (bool v) { ParasiteHostView.ImprintLayers.ShowPigmentDrain = v; });
            AddAnim("İkon switch: ALL imprint switches back on",
                "ikon anahtarı: TÜM iz anahtarları geri açık",
                delegate
                {
                    ParasiteHostView.ImprintLayers.AllOn();
                    animLastLabel = Loc.Pick("every imprint switch back on",
                        "tüm iz anahtarları açık");
                    if (AnimLabOpen)
                    {
                        RedrawAnimationLab();
                    }
                });
        }

        private void AddImprint(AnimImprintScene scene, string en, string tr)
        {
            AddAnim("İkon " + en, "ikon " + tr, delegate { AnimImprint(scene, true); });
        }

        private void AddImprintToggle(string en, string tr, System.Func<bool> get,
            System.Action<bool> set)
        {
            AddAnim("İkon switch: " + en, "ikon anahtarı: " + tr, delegate
            {
                bool on = !get();
                set(on);
                animLastLabel = Loc.Pick("imprint " + en + ": ", "iz " + tr + ": ") + OnOff(on);
                if (AnimLabOpen)
                {
                    RedrawAnimationLab();
                }
            });
        }

        private void AnimImprint(AnimImprintScene scene, bool resetSwitches)
        {
            StopAnimHost();
            StopAnimPress();
            StopAnimSnake();
            StopAnimBossLift();
            StopAnimRot();
            StopAnimRaw();
            if (IsAttachScene(scene))
            {
                animImprintLast = scene;
            }
            animImprint = StartCoroutine(ImprintRoutine(scene, resetSwitches));
        }

        /// <summary>Called from StopAnimHost, so every way the lab stops a host scene stops this
        /// one too.</summary>
        private void StopAnimImprintRoutine()
        {
            if (animImprint != null)
            {
                StopCoroutine(animImprint);
                animImprint = null;
            }
        }

        private static bool IsAttachScene(AnimImprintScene scene)
        {
            switch (scene)
            {
                case AnimImprintScene.AttachFull:
                case AnimImprintScene.ApproachOnly:
                case AnimImprintScene.DimpleOnly:
                case AnimImprintScene.SinkOnly:
                case AnimImprintScene.GrabOnly:
                case AnimImprintScene.SealOnly:
                case AnimImprintScene.Attach1x:
                case AnimImprintScene.AttachHalf:
                case AnimImprintScene.AttachQuarter:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>Which joker a scene puts in the host.</summary>
        private static string ImprintIconOf(AnimImprintScene scene)
        {
            switch (scene)
            {
                case AnimImprintScene.Yangin: return "yangin";
                case AnimImprintScene.Buzluk: return "buzluk";
                case AnimImprintScene.Hazine: return "hazine";
                case AnimImprintScene.Complex: return "robot_supurge";
                case AnimImprintScene.Simple: return "iade";
                default: return "midas";
            }
        }

        /// <summary>A block colour no card happens to be, for the colour tests - or null for a
        /// scene that uses the board's own cube.</summary>
        private static Color? ImprintBlockColour(AnimImprintScene scene)
        {
            switch (scene)
            {
                case AnimImprintScene.Green: return new Color(0.36f, 0.86f, 0.32f);
                case AnimImprintScene.Pink: return new Color(0.95f, 0.46f, 0.72f);
                case AnimImprintScene.Blue: return new Color(0.28f, 0.52f, 0.95f);
                case AnimImprintScene.VeryLight: return new Color(0.93f, 0.92f, 0.88f);
                case AnimImprintScene.VeryDark: return new Color(0.09f, 0.08f, 0.11f);
                default: return null;
            }
        }

        private static CubeKind ImprintKind(AnimImprintScene scene)
        {
            switch (scene)
            {
                case AnimImprintScene.Obsidian: return CubeKind.Obsidian;
                case AnimImprintScene.Gold: return CubeKind.Gold;
                default: return CubeKind.Water;
            }
        }

        private IEnumerator ImprintRoutine(AnimImprintScene scene, bool resetSwitches)
        {
            ParasiteHostView.Layers.HostLayersOn();
            if (resetSwitches)
            {
                ParasiteHostView.ImprintLayers.AllOn();
            }
            ParasiteHostView.ImprintLayers.AttachRate =
                scene == AnimImprintScene.AttachHalf ? 0.5f
                : scene == AnimImprintScene.AttachQuarter ? 0.25f
                : 1f;
            if (scene == AnimImprintScene.RawMembrane)
            {
                // The film on its own: no icon, and no colour seed standing in for one.
                ParasiteHostView.Layers.ShowEssence = false;
            }
            if (scene == AnimImprintScene.DimpleOnly)
            {
                // Only the film's answer: the icon and its fibers stay out of it.
                ParasiteHostView.ImprintLayers.ShowEmbeddedIcon = false;
                ParasiteHostView.ImprintLayers.ShowFiberBack = false;
                ParasiteHostView.ImprintLayers.ShowFiberFront = false;
            }
            if (scene == AnimImprintScene.SinkOnly)
            {
                ParasiteHostView.ImprintLayers.ShowFiberBack = false;
                ParasiteHostView.ImprintLayers.ShowFiberFront = false;
            }

            RoundEngine round = session != null ? session.CurrentRound : null;
            if (round == null || round.Board == null || boardView == null)
            {
                animImprint = null;
                yield break;
            }
            int w = Mathf.Max(7, round.Board.Width);
            int h = Mathf.Max(7, round.Board.Height);
            var board = new GameBoard(w, h);
            List<int> cards = AnimBossCards();
            var centre = new GridPos(w / 2, h / 2);
            ParasiteHostView view = boardView.Parasite;

            // ---- which cells are hosts, and who rides each ----
            var cells = new List<GridPos>();
            var icons = new List<string>();
            var stickers = new List<bool>();
            if (scene == AnimImprintScene.Grid)
            {
                for (int i = 0; i < AnimImprintGrid.Length; i++)
                {
                    cells.Add(new GridPos(centre.X - 2 + i % 5, centre.Y + (i < 5 ? 1 : -1)));
                    icons.Add(AnimImprintGrid[i]);
                    stickers.Add(false);
                }
            }
            else if (scene == AnimImprintScene.SideBySide)
            {
                cells.Add(new GridPos(centre.X - 1, centre.Y));
                icons.Add("midas");
                stickers.Add(true);
                cells.Add(new GridPos(centre.X + 1, centre.Y));
                icons.Add("midas");
                stickers.Add(false);
            }
            else
            {
                cells.Add(centre);
                icons.Add(scene == AnimImprintScene.RawMembrane ? null : ImprintIconOf(scene));
                stickers.Add(scene == AnimImprintScene.RawIconOnTop);
            }

            // ---- the board: the hosts are real protected cubes, the rest the usual fill ----
            Color? fabricated = ImprintBlockColour(scene);
            var reserved = new HashSet<GridPos>(cells);
            CubeKind kind = ImprintKind(scene);
            for (int i = 0; i < cells.Count; i++)
            {
                if (fabricated.HasValue)
                {
                    continue; // the host draws its own block; the cell stays empty under it
                }
                board.SetCubeAt(cells[i], AnimCardCube(cells[i].X, cells[i].Y, cards));
                if (kind != CubeKind.Normal)
                {
                    board.SetCubeKind(cells[i], kind);
                }
                board.SetCubeProtected(cells[i]);
            }
            AnimRotFill(board, cards, 22u, reserved);
            boardView.Rebuild(board, MainBoardWorldSize, MainBoardCenter);

            var live = new List<ParasiteHostView.Host>();
            view.Stop();
            for (int i = 0; i < cells.Count; i++)
            {
                ClusterBurstView.Look look;
                if (fabricated.HasValue)
                {
                    Sprite tile = ViewUtil.CubeTile(CubeKind.Normal);
                    look = new ClusterBurstView.Look
                    {
                        Tile = tile,
                        Colour = ViewUtil.CubeTileColor(tile, fabricated.Value),
                        Paint = fabricated.Value
                    };
                }
                else
                {
                    Cube? cube = board.GetCube(cells[i]);
                    look = cube.HasValue ? LookOf(cube.Value) : new ClusterBurstView.Look();
                }
                string defId = icons[i];
                live.Add(new ParasiteHostView.Host
                {
                    Cell = cells[i],
                    Look = look,
                    DrawBase = fabricated.HasValue,
                    Passenger = new BoundJokerIdentity
                    {
                        Bound = true,
                        InstanceId = i + 1,
                        DefId = defId ?? "tamagotchi",
                        DisplayName = defId ?? "tamagotchi"
                    }
                });
                if (defId != null)
                {
                    view.SetRiderAt(cells[i], ViewUtil.JokerIcon(defId), stickers[i]);
                }
            }
            bool attach = IsAttachScene(scene);
            view.Sync(boardView, live);
            if (attach)
            {
                PlayImprintAttach(scene, view, cells[0]);
            }
            animLastLabel = ImprintLabel(scene, icons[0], live[0]);
            if (AnimLabOpen)
            {
                RedrawAnimationLab();
            }
            if (attach)
            {
                animImprint = null;
                yield break;
            }
            // The rest start from a host that has seated.
            yield return new WaitForSeconds(ParasiteHostView.Style.SeatTotal + 0.1f);

            GridPos host = cells[0];
            switch (scene)
            {
                case AnimImprintScene.Struggle:
                    for (int i = 0; i < 4; i++)
                    {
                        view.ForceStruggle(host);
                        yield return new WaitForSeconds(ParasiteHostView.Style.IdleDuration + 0.7f);
                    }
                    break;
                case AnimImprintScene.Wave:
                    for (int i = 0; i < 3; i++)
                    {
                        view.ForceWave(host);
                        yield return new WaitForSeconds(
                            ParasiteHostView.ImprintStyle.WaveDuration + 0.9f);
                    }
                    break;
                case AnimImprintScene.Destroyed:
                    FlashLine(board, host.Y, true);
                    yield return new WaitForSeconds(0.12f);
                    PlayParasiteSeveranceScene(host, true);
                    break;
                case AnimImprintScene.Resist:
                    // THE REAL RULE: the board refuses, and writes the refusal down itself.
                    board.HostRefusals.Clear();
                    board.DestroyCube(host);
                    PlayHostRefusals(board);
                    yield return new WaitForSeconds(ParasiteHostView.Style.ClampDuration + 0.5f);
                    board.HostRefusals.Clear();
                    board.SetForcedStep(new GridPos(1, 0));
                    board.DestroyCubeForced(host);
                    PlayHostRefusals(board);
                    break;
                default:
                    break; // the still scenes: the imprint standing there is the entry
            }
            animImprint = null;
        }

        /// <summary>
        /// Starts the assimilation on the lab's host the way the market panel does: from a SOURCE
        /// off the board - a point up and to the left of it, where a joker's icon sits in the
        /// panel's first column - at about the size it has there. The phase scenes start and hold
        /// on the timeline's own marks.
        /// </summary>
        private void PlayImprintAttach(AnimImprintScene scene, ParasiteHostView view, GridPos cell)
        {
            float board = MainBoardWorldSize;
            Vector2 world = MainBoardCenter + new Vector2(-board * 0.42f, board * 0.46f);
            Vector3 local = view.transform.InverseTransformPoint(new Vector3(world.x, world.y, 0f));
            Vector2? source = new Vector2(local.x, local.y);
            float size = boardView.CubeWorldSize * 0.9f;
            switch (scene)
            {
                case AnimImprintScene.ApproachOnly:
                    view.PlayAttachPhase(cell, source, size, ParasiteHostView.AttachPhase.Start,
                        ParasiteHostView.AttachPhase.Land);
                    break;
                case AnimImprintScene.DimpleOnly:
                    view.PlayAttachPhase(cell, null, size, ParasiteHostView.AttachPhase.Dimple,
                        ParasiteHostView.AttachPhase.Grab);
                    break;
                case AnimImprintScene.SinkOnly:
                    view.PlayAttachPhase(cell, null, size, ParasiteHostView.AttachPhase.Land,
                        ParasiteHostView.AttachPhase.Grab);
                    break;
                case AnimImprintScene.GrabOnly:
                    view.PlayAttachPhase(cell, null, size, ParasiteHostView.AttachPhase.Grab,
                        ParasiteHostView.AttachPhase.Seal);
                    break;
                case AnimImprintScene.SealOnly:
                    view.PlayAttachPhase(cell, null, size, ParasiteHostView.AttachPhase.Seal,
                        ParasiteHostView.AttachPhase.End);
                    break;
                default:
                    view.PlayAttach(cell, source, size);
                    break;
            }
        }

        private static string ImprintLabel(AnimImprintScene scene, string icon,
            ParasiteHostView.Host host)
        {
            string block = ImprintBlockColour(scene).HasValue
                ? Loc.Pick("fabricated colour", "üretilmiş renk")
                : ViewUtil.KindLabel(ImprintKind(scene));
            string rider = icon ?? Loc.Pick("no icon", "ikon yok");
            string rate = ParasiteHostView.ImprintLayers.AttachRate < 0.99f
                ? "  " + ParasiteHostView.ImprintLayers.AttachRate + "x"
                : "";
            return Loc.Pick("imprint: " + block + " host, rider \"" + rider + "\"" + rate,
                "iz: " + block + " konak, yolcu \"" + rider + "\"" + rate);
        }
    }
}
