// PURPOSE: GameUiController's "Rüzgar" seams - aiming the gust (press where it starts, drag the
// way it blows, let go; or click, then click where it goes), marking what it will move while it is
// aimed, playing it once the rules have run (WindGustView, plus the board's own water animation),
// and the animation lab's wind scenes.
//
// The aim asks the RULES for everything it draws: the band is RuzgarPower.GustFor, the marks are
// RuzgarPower.ThrowsEmbers / WaterWillSlide and Enfeksiyon's WindCarriesOn, and whether the gust
// may blow at all is PowerInventory.CanUse - so a grey lane is exactly a gust the rules refuse.

using System.Collections;
using System.Collections.Generic;
using ProjectBlock.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectBlock.View
{
    partial class GameUiController
    {
        private WindAimView windAim;
        private WindGustView windGust;

        /// <summary>The cell the gust starts from, once the player has pressed on the board.</summary>
        private GridPos? windStart;

        /// <summary>The board that press landed on (the mirror world's, when it did).</summary>
        private BoardView windView;
        private bool windOnMirror;

        /// <summary>The button is still down from the press that set the start.</summary>
        private bool windDragging;

        /// <summary>The last stroke let go of touched nothing - the hint says so.</summary>
        private bool windRefused;

        private void EnsureWind()
        {
            if (windAim == null)
            {
                windAim = new GameObject("WindAim").AddComponent<WindAimView>();
                windAim.Hide();
            }
            if (windGust == null)
            {
                windGust = new GameObject("WindGust").AddComponent<WindGustView>();
                windGust.Sounded = delegate(WindGustView.Cue cue)
                {
                    if (sfx == null)
                    {
                        return;
                    }
                    if (cue == WindGustView.Cue.Gust)
                    {
                        sfx.Whoosh();
                    }
                    else if (cue == WindGustView.Cue.Ignite)
                    {
                        sfx.Flame();
                    }
                };
            }
        }

        /// <summary>Forgets a half-made aim. Called from CancelTargeting and when the gust fires.</summary>
        private void ClearWindAim()
        {
            windStart = null;
            windView = null;
            windOnMirror = false;
            windDragging = false;
            windRefused = false;
            if (windAim != null)
            {
                windAim.Hide();
            }
        }

        /// <summary>
        /// One frame of aiming "Rüzgar". Returns true when the power is a wind (it owns the frame).
        /// Press on the board: the start. Drag and let go: the gust blows that way - if it would
        /// touch anything. A press and release on the same spot keeps the start and waits for a
        /// second click to say where it blows, which is the pad's and a careful player's way.
        /// </summary>
        private bool HandleWindAim(Power power, Vector2 world, Mouse mouse)
        {
            if (!(power is RuzgarPower))
            {
                return false;
            }
            EnsureWind();
            RoundEngine round = session.CurrentRound;
            if (round == null)
            {
                CancelTargeting();
                return true;
            }
            bool pressed = mouse.leftButton.wasPressedThisFrame;
            bool released = mouse.leftButton.wasReleasedThisFrame;

            if (!windStart.HasValue)
            {
                if (pressed && !BeginWindAt(world))
                {
                    CancelTargeting();
                }
                return true;
            }

            ActivationTarget target;
            WindGust gust = WindAimAt(world, out target);
            bool valid = gust != null && gust.Valid;
            bool runnable = valid && session.Powers.CanUse(power.InstanceId, target);
            if (valid)
            {
                windAim.Show(windView, gust, runnable, WindMarks(gust));
            }
            else
            {
                windAim.ShowStart(windView, windStart.Value);
            }

            if (windDragging)
            {
                if (released)
                {
                    windDragging = false;
                    if (runnable)
                    {
                        FireWind(power, target);
                        return true;
                    }
                    // A real stroke into empty air: say so, and keep the start for another try.
                    if (windRefused != valid)
                    {
                        windRefused = valid;
                        UpdateHud();
                    }
                }
                return true;
            }
            if (pressed)
            {
                if (runnable)
                {
                    FireWind(power, target);
                    return true;
                }
                // Anywhere else: a new stroke from where it was pressed.
                if (!BeginWindAt(world))
                {
                    CancelTargeting();
                }
            }
            return true;
        }

        /// <summary>Starts a stroke on whichever world's board was pressed. False off both.</summary>
        private bool BeginWindAt(Vector2 world)
        {
            GridPos cell;
            BoardView on = null;
            bool mirror = false;
            if (boardView.TryWorldToCell(world, out cell))
            {
                on = boardView;
            }
            else
            {
                RoundEngine round = session.CurrentRound;
                if (round != null && round.HasMirrorWorld && mirrorBoardView != null
                    && mirrorBoardView.TryWorldToCell(world, out cell))
                {
                    on = mirrorBoardView;
                    mirror = true;
                }
            }
            if (on == null)
            {
                return false;
            }
            windView = on;
            windOnMirror = mirror;
            windStart = cell;
            windDragging = true;
            windRefused = false;
            windAim.ShowStart(on, cell);
            UpdateHud();
            return true;
        }

        /// <summary>The gust from the start to the pointer, as the rules would blow it.</summary>
        private WindGust WindAimAt(Vector2 world, out ActivationTarget target)
        {
            Vector2 end = windView.WorldToBoardPoint(world);
            var stroke = new BoardStroke(windStart.Value.X, windStart.Value.Y, end.x, end.y);
            target = ActivationTarget.Swipe(stroke).OnWorld(windOnMirror);
            return RuzgarPower.GustFor(windView.Board, target);
        }

        /// <summary>What the gust would move - the rules' own answer, cell by cell.</summary>
        private List<WindAimView.Mark> WindMarks(WindGust gust)
        {
            var marks = new List<WindAimView.Mark>();
            GameBoard board = windView != null ? windView.Board : null;
            if (board == null || gust == null)
            {
                return marks;
            }
            foreach (GridPos cell in gust.Cells)
            {
                if (RuzgarPower.ThrowsEmbers(board, gust, cell))
                {
                    marks.Add(new WindAimView.Mark { Cell = cell, Kind = WindAimView.MarkKind.Fire });
                }
            }
            // Only water that ends up somewhere else - blown uphill it only falls back.
            foreach (GridPos cell in RuzgarPower.WaterThatMoves(board, gust))
            {
                marks.Add(new WindAimView.Mark { Cell = cell, Kind = WindAimView.MarkKind.Water });
            }
            // A joker's marks ride the MAIN world only, as they do in the rules.
            if (!windOnMirror)
            {
                foreach (Joker joker in session.Jokers.Jokers)
                {
                    var infection = joker as EnfeksiyonJoker;
                    if (infection == null)
                    {
                        continue;
                    }
                    foreach (WindCarry carry in infection.WindCarriesOn(board, gust))
                    {
                        marks.Add(new WindAimView.Mark { Cell = carry.From, Kind = WindAimView.MarkKind.Infection });
                    }
                }
            }
            return marks;
        }

        private void FireWind(Power power, ActivationTarget target)
        {
            ClearWindAim();
            RunPowerActivation(power, target);
        }

        /// <summary>The hint line while a wind is aimed.</summary>
        private string WindStep()
        {
            if (!windStart.HasValue)
            {
                return Loc.Pick("press where the gust starts and drag the way it blows",
                    "rüzgarın başlayacağı yere bas ve eseceği yöne sürükle");
            }
            if (windRefused)
            {
                return Loc.Pick("that gust touches nothing it can carry - aim it over fire, water or an infection",
                    "bu rüzgar taşıyabileceği hiçbir şeye değmiyor - ateşin, suyun ya da enfeksiyonun üstünden geçir");
            }
            return windDragging
                ? Loc.Pick("let go where it should blow", "rüzgarın eseceği yerde bırak")
                : Loc.Pick("now click where it blows (or press somewhere else to start again)",
                    "şimdi eseceği yere tıkla (ya da başka bir yere basıp yeniden başla)");
        }

        /// <summary>Every cube in the lane as the board shows it NOW - the faces the gust's
        /// catches burn FROM, taken before the rules turn them to fire.</summary>
        private Dictionary<GridPos, WindGustView.Face> CaptureWindFaces(ActivationTarget target)
        {
            var faces = new Dictionary<GridPos, WindGustView.Face>();
            BoardView on = target.OnMirrorWorld && mirrorBoardView != null ? mirrorBoardView : boardView;
            WindGust gust = on != null ? RuzgarPower.GustFor(on.Board, target) : null;
            if (gust == null || !gust.Valid)
            {
                return faces;
            }
            foreach (GridPos cell in gust.Cells)
            {
                Sprite tile;
                Color colour;
                if (on.TryCubeLook(cell, 0f, out tile, out colour))
                {
                    faces[cell] = new WindGustView.Face { Tile = tile, Colour = colour };
                }
            }
            return faces;
        }

        /// <summary>
        /// Plays the gust the rules just blew: the wind's own sequence, the board's water animation
        /// for the push and the fall (one set of frames from Core, push first), and - once the
        /// water has come to rest - any line the gust completed. True when it played.
        /// </summary>
        private bool PlayWind(RuzgarPower power, ActivationTarget target,
            Dictionary<GridPos, WindGustView.Face> faces)
        {
            WindVisuals report = power.LastGust;
            if (report == null)
            {
                return false;
            }
            EnsureWind();
            BoardView on = target.OnMirrorWorld && mirrorBoardView != null ? mirrorBoardView : boardView;
            windGust.Play(on, report, faces);
            RoundEngine round = session.CurrentRound;
            var blast = round != null
                ? new List<GridPos>(round.ExternalDestructionLog)
                : new List<GridPos>();
            List<IReadOnlyList<WaterMove>> flow = round != null && round.ExternalWaterFrames.Count > 0
                ? new List<IReadOnlyList<WaterMove>>(round.ExternalWaterFrames)
                : null;
            StartCoroutine(WindAftermath(on, flow, blast));
            return true;
        }

        private IEnumerator WindAftermath(BoardView on, List<IReadOnlyList<WaterMove>> flow,
            List<GridPos> blast)
        {
            // The water leaves as the front reaches it - a beat in, not on the first frame.
            yield return new WaitForSeconds(0.1f);
            if (flow != null && on != null)
            {
                bool done = false;
                waterAnimating = true;
                on.PlayWaterAnimation(flow, delegate
                {
                    waterAnimating = false;
                    done = true;
                });
                float guard = 4f;
                while (!done && guard > 0f)
                {
                    guard -= Time.deltaTime;
                    yield return null;
                }
            }
            else
            {
                yield return new WaitForSeconds(0.3f);
            }
            if (blast != null && blast.Count > 0)
            {
                PlayPowerBlast(blast);
                // "Hazine": a mark the gust's line blew open is revealed as the blast breaks it.
                if (session != null && session.Phase == GamePhase.Round)
                {
                    SyncHazine(session.CurrentRound, null);
                }
            }
        }

        private void StopWind()
        {
            ClearWindAim();
            if (windGust != null)
            {
                windGust.Stop();
            }
        }

        // ================================================================== the lab

        // 'F' fire, 'W' water, 'N' a plain block, 'O' obsidian, 'I' a plain block carrying an
        // infection, '.' empty. Row 0 of the array is the TOP of the board.
        private static readonly string[] WindLabFire =
        {
            ".......",
            "..N.N..",
            ".F.N.N.",
            "FF.NN.N",
            ".F.N.N.",
            "..N..N.",
            ".......",
        };

        private static readonly string[] WindLabWater =
        {
            "W......",
            "W......",
            "NN.....",
            "NNW..N.",
            "NN.....",
            "NN.....",
            "NNNN.NN",
        };

        private static readonly string[] WindLabMixed =
        {
            ".......",
            ".W...N.",
            "F.N.N..",
            "FN.O.NN",
            ".W..N..",
            "..N...N",
            "NNNN.NN",
        };

        private static readonly string[] WindLabDiagonal =
        {
            "......F",
            ".....FF",
            "...N.N.",
            "..N.N..",
            ".N.N...",
            "N.N....",
            ".......",
        };

        private static readonly string[] WindLabInfection =
        {
            ".......",
            ".......",
            ".......",
            "I..N.N.",
            ".......",
            ".......",
            ".......",
        };

        private void AddWindLab()
        {
            AddAnimSub("powers", "wind", "rüzgar", "rüzgar");
            AddAnim("rüzgar: fire - embers downwind (level gust)", "rüzgar: ateş - rüzgar yönüne kıvılcım (yatay)",
                () => PlayWindLab(WindLabFire, 0f, 3f, 6f, 3f, 60, 2));
            AddAnim("rüzgar: fire - every ember catches", "rüzgar: ateş - her kıvılcım tutuşturur",
                () => PlayWindLab(WindLabFire, 0f, 3f, 6f, 3f, 100, 3));
            AddAnim("rüzgar: fire - every ember is carried off", "rüzgar: ateş - hepsi uçup gider",
                () => PlayWindLab(WindLabFire, 0f, 3f, 6f, 3f, 0, 2));
            AddAnim("rüzgar: water - pushed until it hits something, then falls",
                "rüzgar: su - bir engele kadar sürüklenir, sonra düşer",
                () => PlayWindLab(WindLabWater, 0f, 5f, 3f, 5f, 60, 2));
            AddAnim("rüzgar: water - blown at an angle", "rüzgar: su - eğik rüzgar",
                () => PlayWindLab(WindLabWater, 0f, 6f, 4f, 3.6f, 60, 2));
            AddAnim("rüzgar: mixed - fire, water and stone", "rüzgar: karışık - ateş, su ve taş",
                () => PlayWindLab(WindLabMixed, 0f, 3f, 6f, 2.5f, 60, 2));
            AddAnim("rüzgar: the longest gust, corner to corner", "rüzgar: en uzun rüzgar, köşeden köşeye",
                () => PlayWindLab(WindLabDiagonal, 6f, 6f, 0f, 0f, 60, 2));
            AddAnim("rüzgar: an infection carried to the next block", "rüzgar: enfeksiyon bir sonraki bloğa taşınır",
                () => PlayWindLab(WindLabInfection, 0f, 3f, 6f, 3f, 60, 2));
            AddAnim("rüzgar: + fire at 0.25x", "rüzgar: + ateş 0.25x", () =>
            {
                PlayWindLab(WindLabFire, 0f, 3f, 6f, 3f, 100, 3);
                Time.timeScale = 0.25f;
            });
            AddAnim("rüzgar: + the AIM over the mixed board (stays up)", "rüzgar: + karışık tahtada NİŞAN (açık kalır)",
                () => ShowWindLabAim(WindLabMixed, 0f, 3f, 6f, 2.5f));
            AddAnim("rüzgar: + an aim that touches nothing (grey)", "rüzgar: + hiçbir şeye değmeyen nişan (gri)",
                () => ShowWindLabAim(WindLabMixed, 6f, 0f, 6f, 6f));
        }

        private GameBoard BuildWindLabBoard(string[] rows, out List<GridPos> infected)
        {
            AnimResync();
            infected = new List<GridPos>();
            int h = rows.Length;
            int w = rows[0].Length;
            var board = new GameBoard(w, h);
            List<int> cards = AnimBossCards();
            for (int r = 0; r < h; r++)
            {
                for (int x = 0; x < w; x++)
                {
                    char c = rows[r][x];
                    int y = h - 1 - r;
                    int card = cards.Count > 0 ? cards[(x + y * 3) % cards.Count] : 1;
                    CubeKind? kind = c == 'F' ? CubeKind.Fire : c == 'W' ? CubeKind.Water
                        : c == 'N' || c == 'I' ? CubeKind.Normal : c == 'O' ? CubeKind.Obsidian
                        : (CubeKind?)null;
                    if (kind.HasValue)
                    {
                        board.SetCubeAt(new GridPos(x, y), new Cube(kind.Value, card));
                    }
                    if (c == 'I')
                    {
                        infected.Add(new GridPos(x, y));
                    }
                }
            }
            boardView.Rebuild(board, MainBoardWorldSize, MainBoardCenter);
            boardView.Refresh();
            return board;
        }

        /// <summary>A gust on a board of the lab's own, through the power's own rule
        /// (RuzgarPower.BlowOn) and the board's own gravity, played by the game's own view.</summary>
        private void PlayWindLab(string[] rows, float fx, float fy, float tx, float ty, int catchPercent,
            int embersPerFire)
        {
            List<GridPos> infected;
            GameBoard board = BuildWindLabBoard(rows, out infected);
            EnsureWind();
            var target = ActivationTarget.Swipe(new BoardStroke(fx, fy, tx, ty));
            WindGust gust = RuzgarPower.GustFor(board, target);
            if (gust == null || !gust.Valid)
            {
                animLastLabel = "the lab stroke is not a wind";
                return;
            }
            Dictionary<GridPos, WindGustView.Face> faces = CaptureWindFaces(target);
            WindVisuals report = RuzgarPower.BlowOn(board, gust, new SeededRandom(4242), embersPerFire,
                catchPercent);
            var flow = new List<IReadOnlyList<WaterMove>>(report.PushFrames);
            board.SettleWaterAndReact(flow);
            // An infection carried the way Enfeksiyon carries it: the first block downwind.
            var infections = new List<InfectedCell>();
            foreach (GridPos source in infected)
            {
                infections.Add(new InfectedCell(source, 0, 3));
                foreach (GridPos cell in gust.Cells)
                {
                    if (gust.IsAhead(source, cell) && board.GetCube(cell).HasValue && !infected.Contains(cell))
                    {
                        gust.Carries.Add(new WindCarry { From = source, To = cell, CarrierId = "enfeksiyon" });
                        infections.Add(new InfectedCell(cell, 0, 3));
                        break;
                    }
                }
            }
            boardView.Refresh();
            if (infections.Count > 0)
            {
                boardView.ShowInfections(infections);
            }
            windGust.Play(boardView, report, faces);
            StartCoroutine(WindAftermath(boardView, flow.Count > 0 ? flow : null, null));
            animLastLabel = "embers " + report.Embers.Count + " (lit " + report.Ignitions.Count + ")  water "
                + report.Pushes.Count + "  carried " + gust.Carries.Count + "  length " + gust.Length.ToString("0.0");
        }

        /// <summary>The aim, held up over a lab board - no input, so it can be looked at.</summary>
        private void ShowWindLabAim(string[] rows, float fx, float fy, float tx, float ty)
        {
            List<GridPos> infected;
            GameBoard board = BuildWindLabBoard(rows, out infected);
            EnsureWind();
            var target = ActivationTarget.Swipe(new BoardStroke(fx, fy, tx, ty));
            WindGust gust = RuzgarPower.GustFor(board, target);
            windView = boardView;
            windOnMirror = true; // the lab board has no jokers riding it
            var marks = WindMarks(gust);
            windView = null;
            windOnMirror = false;
            windAim.Show(boardView, gust, RuzgarPower.TouchesBoard(board, gust), marks);
            animLastLabel = "cells " + (gust != null ? gust.Cells.Count : 0) + "  marked " + marks.Count;
        }
    }
}
