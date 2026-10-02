// PURPOSE: GameUiController's "Rüzgar" seams - the AIM (select the power: the board takes a breath
// of emphasis and a pressure knot rides the pointer; press: the origin is pinned; drag: the storm's
// own corridor follows; let go: it blows - or click, then click; or two A presses on a pad), the
// CAST (WindStormView, once the rules have run), and the animation lab's RÜZGAR / DIRECTED STORM
// section.
//
// The aim asks the RULES for everything it draws: PowerInventory.PreviewWind is the lane, the cap,
// what is affected and how; PowerInventory.CanUse is whether it may blow. A grey lane is exactly a
// gust the rules refuse, and nothing here measures a cell against the band.
// The cast is WindVisuals, played once by identity; the only thing gathered here is the FACES of
// the cubes in the lane before the rules run (a block burns from the face it had).
// The lab runs the power's own statics (RuzgarPower.BlowOn / SettleOn / PreviewOn) on boards of
// its own, and plays them through the same two views the game uses.

using System;
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
        private WindStormView windStorm;

        /// <summary>The cell the gust starts from, once the player has pressed on the board.</summary>
        private GridPos? windStart;

        /// <summary>The board that press landed on (the mirror world's, when it did).</summary>
        private BoardView windView;
        private bool windOnMirror;

        /// <summary>The button is still down from the press that set the start.</summary>
        private bool windDragging;

        /// <summary>The last stroke let go of touched nothing - the hint says so.</summary>
        private bool windRefused;

        /// <summary>The power being aimed, from the frame aim mode was entered.</summary>
        private int? windArmedFor;

        private Coroutine windLabRoutine;
        private readonly WindFx.Quality windDefaultQuality = WindFx.Level;

        private float WindPixel()
        {
            return cam != null ? cam.orthographicSize * 2f / Mathf.Max(1, Screen.height) : 0.01f;
        }

        private void EnsureWind()
        {
            if (windAim == null)
            {
                windAim = new GameObject("WindAim").AddComponent<WindAimView>();
                windAim.Pixel = WindPixel;
            }
            if (windStorm == null)
            {
                windStorm = new GameObject("WindStorm").AddComponent<WindStormView>();
                windStorm.Pixel = WindPixel;
                windStorm.Sounded = delegate(WindCue cue, float travel)
                {
                    if (sfx != null)
                    {
                        sfx.Wind(cue, travel);
                    }
                };
                // The haptic beats are announced; the game has no haptics layer to send them to.
                windStorm.Haptic = delegate(WindHaptic beat) { };
            }
        }

        // ================================================================== aim mode

        /// <summary>Entering aim mode: the power's card answers, the board takes a few percent of
        /// emphasis off the backdrop, and the aim's quiet bed comes up.</summary>
        private void EnterWindAim(Power power)
        {
            windArmedFor = power.InstanceId;
            if (powerBar != null)
            {
                powerBar.PulsePower(power.InstanceId);
            }
            SetWindMood(true);
        }

        private void SetWindMood(bool on)
        {
            if (background == null)
            {
                return;
            }
            if (!on)
            {
                background.SetMood("wind", null);
                return;
            }
            GameBackgroundPresentationController.Mood mood = GameBackgroundPresentationController.Mood.Neutral;
            mood.Brightness = 0.968f;
            background.SetMood("wind", mood);
        }

        /// <summary>Every frame: keeps the bed with the drag, and puts aim mode away if the aim was
        /// dropped without a cancel (another power armed over it, the round ending).</summary>
        private void TickWind()
        {
            if (!windArmedFor.HasValue)
            {
                return;
            }
            if (pendingTargetPowerId != windArmedFor)
            {
                ClearWindAim(false);
                return;
            }
            if (sfx != null && windAim != null)
            {
                sfx.WindBed(1f, windAim.Drawn);
            }
        }

        /// <summary>Forgets a half-made aim (CancelTargeting calls this).</summary>
        private void ClearWindAim()
        {
            ClearWindAim(false);
        }

        /// <summary>Leaves aim mode. Cast: the lane is handed to the storm on this frame.
        /// Otherwise it folds back into its origin with a small release of air.</summary>
        private void ClearWindAim(bool cast)
        {
            bool wasAiming = windArmedFor.HasValue;
            bool hadOrigin = windStart.HasValue;
            windStart = null;
            windView = null;
            windOnMirror = false;
            windDragging = false;
            windRefused = false;
            windArmedFor = null;
            if (windAim != null)
            {
                if (cast || !hadOrigin)
                {
                    windAim.Hide();
                }
                else
                {
                    windAim.Cancel();
                }
            }
            if (wasAiming)
            {
                SetWindMood(false);
                if (sfx != null)
                {
                    sfx.WindBed(0f, 0f);
                    if (!cast && hadOrigin)
                    {
                        sfx.Wind(WindCue.Cancel, 0f);
                    }
                }
            }
        }

        /// <summary>
        /// One frame of aiming "Rüzgar". Returns true when the power is a wind (it owns the frame).
        /// Press on the board: the origin. Drag and let go: the gust blows that way - if the rules
        /// would blow it. A press and release on the same spot keeps the origin and waits for a
        /// second click to say where it blows.
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
            if (windArmedFor != power.InstanceId)
            {
                EnterWindAim(power);
            }
            bool pressed = mouse.leftButton.wasPressedThisFrame;
            bool released = mouse.leftButton.wasReleasedThisFrame;

            if (!windStart.HasValue)
            {
                // the knot rides the pointer while it is over a board
                BoardView under = WindBoardAt(world);
                if (under != null)
                {
                    windAim.Cursor(under, world);
                }
                else
                {
                    windAim.Hide();
                }
                if (pressed && !BeginWindAt(world))
                {
                    CancelTargeting();
                }
                return true;
            }

            ActivationTarget target;
            WindPreview preview = WindPreviewAt(power, world, out target);
            bool valid = preview != null && preview.Gust != null && preview.Gust.Valid;
            bool usable = valid && preview.Valid && session.Powers.CanUse(power.InstanceId, target);
            windAim.Aim(windView, preview, usable);

            if (windDragging)
            {
                if (released)
                {
                    windDragging = false;
                    if (usable)
                    {
                        FireWind(power, target);
                        return true;
                    }
                    if (valid)
                    {
                        RefuseWind(preview);
                    }
                }
                return true;
            }
            if (pressed)
            {
                if (usable)
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

        /// <summary>A real stroke the rules will not blow: the nose folds, a small word says why.</summary>
        private void RefuseWind(WindPreview preview)
        {
            string why = preview != null && preview.Reason == WindRefusal.NothingToCarry
                ? Loc.Pick("nothing here to carry", "taşıyacak bir şey yok")
                : Loc.Pick("not now", "şimdi olmaz");
            windAim.Refuse(why);
            if (sfx != null)
            {
                sfx.Wind(WindCue.Refused, 0f);
            }
            if (!windRefused)
            {
                windRefused = true;
                UpdateHud();
            }
        }

        /// <summary>The board under a world point - the main one, or the mirror world's.</summary>
        private BoardView WindBoardAt(Vector2 world)
        {
            GridPos cell;
            if (boardView != null && boardView.TryWorldToCell(world, out cell))
            {
                return boardView;
            }
            RoundEngine round = session != null ? session.CurrentRound : null;
            if (round != null && round.HasMirrorWorld && mirrorBoardView != null
                && mirrorBoardView.TryWorldToCell(world, out cell))
            {
                return mirrorBoardView;
            }
            return null;
        }

        /// <summary>Pins the origin on whichever world's board was pressed. False off both.</summary>
        private bool BeginWindAt(Vector2 world)
        {
            BoardView on = WindBoardAt(world);
            GridPos cell;
            if (on == null || !on.TryWorldToCell(world, out cell))
            {
                return false;
            }
            windView = on;
            windOnMirror = on == mirrorBoardView && on != boardView;
            windStart = cell;
            windDragging = true;
            windRefused = false;
            windAim.Pin(on, cell);
            if (sfx != null)
            {
                sfx.Wind(WindCue.OriginLock, 0f);
            }
            UpdateHud();
            return true;
        }

        private ActivationTarget WindTargetAt(Vector2 world)
        {
            Vector2 end = windView.WorldToBoardPoint(world);
            var stroke = new BoardStroke(windStart.Value.X, windStart.Value.Y, end.x, end.y);
            return ActivationTarget.Swipe(stroke).OnWorld(windOnMirror);
        }

        /// <summary>What the rules say a stroke to the pointer would do.</summary>
        private WindPreview WindPreviewAt(Power power, Vector2 world, out ActivationTarget target)
        {
            target = WindTargetAt(world);
            return session.Powers.PreviewWind(power.InstanceId, target);
        }

        /// <summary>The gust from the origin to a point, as the rules would blow it (the pad's
        /// second press asks this).</summary>
        private WindGust WindAimAt(Vector2 world, out ActivationTarget target)
        {
            target = WindTargetAt(world);
            return RuzgarPower.GustFor(windView.Board, target);
        }

        private void FireWind(Power power, ActivationTarget target)
        {
            ClearWindAim(true);
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

        // ================================================================== the cast

        /// <summary>Every cube in the lane as the board shows it NOW - the faces the storm's
        /// reactions start from, taken before the rules move or burn anything.</summary>
        private Dictionary<GridPos, WindStormView.Face> CaptureWindFaces(ActivationTarget target)
        {
            var faces = new Dictionary<GridPos, WindStormView.Face>();
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
                    faces[cell] = new WindStormView.Face { Tile = tile, Colour = colour };
                }
            }
            return faces;
        }

        /// <summary>
        /// Plays the gust the rules just blew, and - once its readable part is over - any line it
        /// completed. True when it played. Input is not held for it.
        /// </summary>
        private bool PlayWind(RuzgarPower power, ActivationTarget target,
            Dictionary<GridPos, WindStormView.Face> faces)
        {
            WindVisuals report = power.LastGust;
            if (report == null)
            {
                return false;
            }
            EnsureWind();
            BoardView on = target.OnMirrorWorld && mirrorBoardView != null ? mirrorBoardView : boardView;
            windStorm.Play(on, report, faces);
            RoundEngine round = session.CurrentRound;
            var blast = round != null
                ? new List<GridPos>(round.ExternalDestructionLog)
                : new List<GridPos>();
            if (blast.Count > 0)
            {
                StartCoroutine(WindAftermath(windStorm.HeroSeconds, blast));
            }
            return true;
        }

        private IEnumerator WindAftermath(float delay, List<GridPos> blast)
        {
            yield return new WaitForSeconds(Mathf.Max(0.1f, delay));
            PlayPowerBlast(blast);
            // "Hazine": a mark the gust's line blew open is revealed as the blast breaks it.
            if (session != null && session.Phase == GamePhase.Round)
            {
                SyncHazine(session.CurrentRound, null);
            }
        }

        private void StopWind()
        {
            if (windLabRoutine != null)
            {
                StopCoroutine(windLabRoutine);
                windLabRoutine = null;
            }
            ClearWindAim(true);
            if (windStorm != null)
            {
                windStorm.Stop();
            }
            SetWindMood(false);
            if (sfx != null)
            {
                sfx.WindBed(0f, 0f);
            }
            WindFx.Layers.AllOn();
        }

        // ================================================================== the lab

        // 'F' fire, 'W' water, 'N' a plain block, 'O' obsidian, 'I' a plain block carrying an
        // infection, '.' empty. Row 0 of the array is the TOP of the board.
        private static readonly string[] WindLabEmpty =
        {
            ".......", ".......", ".......", ".......", ".......", ".......", ".......",
        };

        private static readonly string[] WindLabBlocks =
        {
            ".......", "..N.N..", ".N.O.N.", "N..N..N", ".N.N.N.", "..N.N..", ".......",
        };

        private static readonly string[] WindLabFireOne =
        {
            ".......", ".......", "...N.N.", "F..N.N.", "...N.N.", ".......", ".......",
        };

        private static readonly string[] WindLabFireThree =
        {
            ".......", "..N.N..", ".F.N.N.", "F..NN.N", ".F.N.N.", "..N..N.", ".......",
        };

        private static readonly string[] WindLabFireMany =
        {
            ".......", "FNNNNN.", ".NNNNN.", "FNNNNNN", ".NNNNN.", "FNNNNN.", ".......",
        };

        private static readonly string[] WindLabWaterStep =
        {
            ".......", ".......", ".......", ".W.N...", "NNN....", ".......", ".......",
        };

        private static readonly string[] WindLabWaterFar =
        {
            ".......", "W......", "NN.....", ".......", ".......", ".......", "NNNNN..",
        };

        private static readonly string[] WindLabWaterMany =
        {
            "W......", "W......", "NN.....", "NNW..N.", "NN.....", "NN.....", "NNNN.N.",
        };

        private static readonly string[] WindLabInfection =
        {
            ".......", ".......", ".......", "I..N.N.", ".......", ".......", ".......",
        };

        private static readonly string[] WindLabFireWater =
        {
            ".......", ".W...N.", "F.N.N..", "FN.O.NN", ".W..N..", "..N...N", "NNNN.N.",
        };

        private static readonly string[] WindLabWaterInfection =
        {
            ".......", ".......", "W......", "NI..N.N", "N......", "N......", "NNNN...",
        };

        private static readonly string[] WindLabFireInfection =
        {
            ".......", ".......", "F..N..N", ".I.N.N.", "F...N..", ".......", ".......",
        };

        private static readonly string[] WindLabMixed =
        {
            ".......", "......N", "....IN.", "..FN.N.", ".W..N..", "NN.....", "NNN.N..",
        };

        private static readonly string[] WindLabDense =
        {
            "WN.NNN.", "NFNNINN", "WNNNNN.", "FNINNNN", "NWNNNN.", "NNFNNNN", "NNNNNN.",
        };

        private void AddWindLab()
        {
            AddAnimSub("powers", "wind", "RÜZGAR / DIRECTED STORM", "RÜZGAR / YÖNLENDİRİLMİŞ FIRTINA");

            // ---- AIM ----
            AddAnim("rüzgar 1: power select (the board's breath, the knot)", "rüzgar 1: güç seçimi (tahtanın nefesi, düğüm)",
                () => WindLabAim(WindLabMixed, 3f, 3f, 3f, 3f, WindLabAimMode.Select));
            AddAnim("rüzgar 2: origin cursor", "rüzgar 2: başlangıç imleci",
                () => WindLabAim(WindLabMixed, 2f, 4f, 2f, 4f, WindLabAimMode.Cursor));
            AddAnim("rüzgar 3: origin lock", "rüzgar 3: başlangıç kilidi",
                () => WindLabAim(WindLabMixed, 1f, 1f, 1f, 1f, WindLabAimMode.Pin));
            AddAnim("rüzgar 4: short drag", "rüzgar 4: kısa sürükleme",
                () => WindLabAim(WindLabMixed, 1f, 3f, 3.2f, 3.4f, WindLabAimMode.Aim));
            AddAnim("rüzgar 5: long drag", "rüzgar 5: uzun sürükleme",
                () => WindLabAim(WindLabMixed, 0f, 3f, 6f, 3.8f, WindLabAimMode.Aim));
            AddAnim("rüzgar 6: max length (the nose stays at the cap)", "rüzgar 6: en fazla uzunluk (burun sınırda kalır)",
                () => WindLabAim(WindLabMixed, 0f, 0f, 9f, 9f, WindLabAimMode.Aim));
            AddAnim("rüzgar 7: rotate direction while dragging", "rüzgar 7: sürüklerken yön çevirme",
                () => WindLabAim(WindLabMixed, 3f, 3f, 6f, 3f, WindLabAimMode.Rotate));
            AddAnim("rüzgar 8: valid corridor", "rüzgar 8: geçerli koridor",
                () => WindLabAim(WindLabMixed, 0f, 2f, 6f, 4.5f, WindLabAimMode.Aim));
            AddAnim("rüzgar 9: invalid corridor (nothing to carry)", "rüzgar 9: geçersiz koridor (taşıyacak bir şey yok)",
                () => WindLabAim(WindLabBlocks, 0f, 3f, 6f, 3f, WindLabAimMode.Refused));
            AddAnim("rüzgar 10: affected-cell preview", "rüzgar 10: etkilenenlerin önizlemesi",
                () => WindLabAim(WindLabDense, 0f, 4f, 6f, 2f, WindLabAimMode.Aim));

            // ---- STORM CORE ----
            AddAnim("rüzgar 11: empty corridor storm", "rüzgar 11: boş koridorda fırtına",
                () => WindLabCast(WindLabEmpty, 0f, 3f, 6f, 3f));
            AddAnim("rüzgar 12: storm front only", "rüzgar 12: yalnız cephe",
                () => WindLabLayer(WindLabBlocks, f: true));
            AddAnim("rüzgar 13: pressure body only", "rüzgar 13: yalnız basınç gövdesi",
                () => WindLabLayer(WindLabBlocks, b: true));
            AddAnim("rüzgar 14: flow ribbons only", "rüzgar 14: yalnız akış şeritleri",
                () => WindLabLayer(WindLabBlocks, r: true));
            AddAnim("rüzgar 15: particles only", "rüzgar 15: yalnız zerreler",
                () => WindLabLayer(WindLabBlocks, m: true));
            AddAnim("rüzgar 16: distortion only", "rüzgar 16: yalnız akış kırılması",
                () => WindLabLayer(WindLabBlocks, d: true));
            AddAnim("rüzgar 17: full storm (over plain blocks)", "rüzgar 17: tam fırtına (düz blokların üstünden)",
                () => WindLabCast(WindLabBlocks, 0f, 3f, 6f, 3f));
            AddAnim("rüzgar 18: short corridor", "rüzgar 18: kısa koridor",
                () => WindLabCast(WindLabBlocks, 2f, 3f, 4f, 3.6f));
            AddAnim("rüzgar 19: board-diagonal corridor", "rüzgar 19: tahta köşegeni",
                () => WindLabCast(WindLabBlocks, 0f, 0f, 6f, 6f));

            // ---- FIRE ----
            AddAnim("rüzgar 20: single fire contact (whole)", "rüzgar 20: tek ateş teması (bütün)",
                () => WindLabCast(WindLabFireOne, 0f, 3f, 6f, 3f));
            AddAnim("rüzgar 21: fire lean", "rüzgar 21: alevin yatması",
                () => WindLabCast(WindLabFireOne, 0f, 3f, 6f, 3f, isolate: WindBeat.FireLean));
            AddAnim("rüzgar 22: ember tear-off", "rüzgar 22: közün kopması",
                () => WindLabCast(WindLabFireOne, 0f, 3f, 6f, 3f, isolate: WindBeat.FireTear));
            AddAnim("rüzgar 23: ember travel", "rüzgar 23: közün taşınması",
                () => WindLabCast(WindLabFireOne, 0f, 3f, 6f, 3f, isolate: WindBeat.FireTravel));
            AddAnim("rüzgar 24: target hot spot", "rüzgar 24: hedefte sıcak nokta",
                () => WindLabCast(WindLabFireOne, 0f, 3f, 6f, 3f, isolate: WindBeat.FireHotSpot));
            AddAnim("rüzgar 25: target ignition", "rüzgar 25: hedefin tutuşması",
                () => WindLabCast(WindLabFireOne, 0f, 3f, 6f, 3f, isolate: WindBeat.FireIgnite));
            AddAnim("rüzgar 26: full fire conversion", "rüzgar 26: ateşe tam dönüşüm",
                () => WindLabCast(WindLabFireOne, 0f, 3f, 6f, 3f, isolate: WindBeat.FireConversion));
            AddAnim("rüzgar 27: 3 fire sources", "rüzgar 27: 3 ateş kaynağı",
                () => WindLabCast(WindLabFireThree, 0f, 3f, 6f, 3f));
            AddAnim("rüzgar 28: multiple target ignition (budget)", "rüzgar 28: çok hedef tutuşması (bütçe)",
                () => WindLabCast(WindLabFireMany, 0f, 3f, 6f, 3f, embers: 3));
            AddAnim("rüzgar 28b: every ember carried off", "rüzgar 28b: hepsi uçup gider",
                () => WindLabCast(WindLabFireThree, 0f, 3f, 6f, 3f, catchPercent: 0));

            // ---- WATER ----
            AddAnim("rüzgar 29: single water contact (whole)", "rüzgar 29: tek su teması (bütün)",
                () => WindLabCast(WindLabWaterFar, 0f, 5f, 4f, 5f));
            AddAnim("rüzgar 30: water pressure deform", "rüzgar 30: suyun basınçla şekil bozması",
                () => WindLabCast(WindLabWaterFar, 0f, 5f, 4f, 5f, isolate: WindBeat.WaterDeform));
            AddAnim("rüzgar 31: water detach", "rüzgar 31: suyun kopması",
                () => WindLabCast(WindLabWaterFar, 0f, 5f, 4f, 5f, isolate: WindBeat.WaterDetach));
            AddAnim("rüzgar 32: water 1-cell drag", "rüzgar 32: su 1 kare sürüklenir",
                () => WindLabCast(WindLabWaterStep, 0f, 3f, 4f, 3f));
            AddAnim("rüzgar 33: water multi-cell drag (one motion, then the fall)", "rüzgar 33: su çok kare sürüklenir (tek hareket, sonra düşüş)",
                () => WindLabCast(WindLabWaterFar, 0f, 5f, 4f, 5f, isolate: WindBeat.WaterDrag));
            AddAnim("rüzgar 34: water arrival", "rüzgar 34: suyun varışı",
                () => WindLabCast(WindLabWaterFar, 0f, 5f, 4f, 5f, isolate: WindBeat.WaterArrival));
            AddAnim("rüzgar 35: multiple water cubes", "rüzgar 35: birden çok su küpü",
                () => WindLabCast(WindLabWaterMany, 0f, 5f, 4f, 3.5f));

            // ---- INFECTION ----
            AddAnim("rüzgar 36: infection contact", "rüzgar 36: enfeksiyona temas",
                () => WindLabCast(WindLabInfection, 0f, 3f, 6f, 3f, isolate: WindBeat.SporeContact));
            AddAnim("rüzgar 37: spore tear-off", "rüzgar 37: sporların kopması",
                () => WindLabCast(WindLabInfection, 0f, 3f, 6f, 3f, isolate: WindBeat.SporeTear));
            AddAnim("rüzgar 38: essence travel", "rüzgar 38: özün taşınması",
                () => WindLabCast(WindLabInfection, 0f, 3f, 6f, 3f, isolate: WindBeat.SporeTravel));
            AddAnim("rüzgar 39: target contamination", "rüzgar 39: hedefin bulaşması",
                () => WindLabCast(WindLabInfection, 0f, 3f, 6f, 3f, isolate: WindBeat.SporeContaminate));
            AddAnim("rüzgar 40: new seed growth", "rüzgar 40: yeni tohumun büyümesi",
                () => WindLabCast(WindLabInfection, 0f, 3f, 6f, 3f, isolate: WindBeat.SporeSeed));
            AddAnim("rüzgar 41: source depletion / recovery", "rüzgar 41: kaynağın tükenip toparlanması",
                () => WindLabCast(WindLabInfection, 0f, 3f, 6f, 3f, isolate: WindBeat.SporeSource));
            AddAnim("rüzgar 42: full infection spread", "rüzgar 42: enfeksiyonun tam yayılması",
                () => WindLabCast(WindLabInfection, 0f, 3f, 6f, 3f));

            // ---- MIXED ----
            AddAnim("rüzgar 43: fire + water", "rüzgar 43: ateş + su",
                () => WindLabCast(WindLabFireWater, 0f, 3f, 6f, 2.5f));
            AddAnim("rüzgar 44: water + infection", "rüzgar 44: su + enfeksiyon",
                () => WindLabCast(WindLabWaterInfection, 0f, 3.5f, 6f, 3.5f));
            AddAnim("rüzgar 45: fire + infection", "rüzgar 45: ateş + enfeksiyon",
                () => WindLabCast(WindLabFireInfection, 0f, 3f, 6f, 3f));
            AddAnim("rüzgar 46: fire + water + infection (one storm)", "rüzgar 46: ateş + su + enfeksiyon (tek fırtına)",
                () => WindLabCast(WindLabMixed, 0f, 1.5f, 6f, 5f));
            AddAnim("rüzgar 47: dense mixed corridor", "rüzgar 47: yoğun karışık koridor",
                () => WindLabCast(WindLabDense, 0f, 5f, 6f, 1.5f, embers: 3));

            // ---- SPEED ----
            AddAnim("rüzgar 48: mixed at 1x", "rüzgar 48: karışık 1x",
                () => WindLabCast(WindLabMixed, 0f, 1.5f, 6f, 5f));
            AddAnim("rüzgar 49: mixed at 0.5x", "rüzgar 49: karışık 0.5x", () =>
            {
                WindLabCast(WindLabMixed, 0f, 1.5f, 6f, 5f);
                Time.timeScale = 0.5f;
            });
            AddAnim("rüzgar 50: mixed at 0.25x", "rüzgar 50: karışık 0.25x", () =>
            {
                WindLabCast(WindLabMixed, 0f, 1.5f, 6f, 5f);
                Time.timeScale = 0.25f;
            });

            // ---- QUALITY ----
            AddAnim("rüzgar 51: quality HIGH", "rüzgar 51: kalite YÜKSEK", () => WindLabQuality(WindFx.Quality.High));
            AddAnim("rüzgar 52: quality MEDIUM", "rüzgar 52: kalite ORTA", () => WindLabQuality(WindFx.Quality.Medium));
            AddAnim("rüzgar 53: quality LOW", "rüzgar 53: kalite DÜŞÜK", () => WindLabQuality(WindFx.Quality.Low));

            // ---- DEBUG ----
            WindLabToggle("ShowWindOrigin", () => WindFx.DebugFlags.ShowWindOrigin, v => WindFx.DebugFlags.ShowWindOrigin = v);
            WindLabToggle("ShowWindEndpoint", () => WindFx.DebugFlags.ShowWindEndpoint, v => WindFx.DebugFlags.ShowWindEndpoint = v);
            WindLabToggle("ShowWindVector", () => WindFx.DebugFlags.ShowWindVector, v => WindFx.DebugFlags.ShowWindVector = v);
            WindLabToggle("ShowWindWidth", () => WindFx.DebugFlags.ShowWindWidth, v => WindFx.DebugFlags.ShowWindWidth = v);
            WindLabToggle("ShowWindMaxLength", () => WindFx.DebugFlags.ShowWindMaxLength, v => WindFx.DebugFlags.ShowWindMaxLength = v);
            WindLabToggle("ShowWindCorridorBounds", () => WindFx.DebugFlags.ShowWindCorridorBounds, v => WindFx.DebugFlags.ShowWindCorridorBounds = v);
            WindLabToggle("ShowWindAffectedCells", () => WindFx.DebugFlags.ShowWindAffectedCells, v => WindFx.DebugFlags.ShowWindAffectedCells = v);
            WindLabToggle("ShowWindAffectedEntities", () => WindFx.DebugFlags.ShowWindAffectedEntities, v => WindFx.DebugFlags.ShowWindAffectedEntities = v);
            WindLabToggle("ShowStormFront", () => WindFx.DebugFlags.ShowStormFront, v => WindFx.DebugFlags.ShowStormFront = v);
            WindLabToggle("ShowPressureBody (off = hidden)", () => WindFx.Layers.Body, v => WindFx.Layers.Body = v);
            WindLabToggle("ShowFlowRibbons (off = hidden)", () => WindFx.Layers.Ribbons, v => WindFx.Layers.Ribbons = v);
            WindLabToggle("ShowWindParticles (off = hidden)", () => WindFx.Layers.Motes, v => WindFx.Layers.Motes = v);
            WindLabToggle("ShowWindDistortion (off = hidden)", () => WindFx.Layers.Distortion, v => WindFx.Layers.Distortion = v);
            WindLabToggle("ShowFireTransferSources", () => WindFx.DebugFlags.ShowFireTransferSources, v => WindFx.DebugFlags.ShowFireTransferSources = v);
            WindLabToggle("ShowFireTransferTargets", () => WindFx.DebugFlags.ShowFireTransferTargets, v => WindFx.DebugFlags.ShowFireTransferTargets = v);
            WindLabToggle("ShowFirePaths", () => WindFx.DebugFlags.ShowFirePaths, v => WindFx.DebugFlags.ShowFirePaths = v);
            WindLabToggle("ShowWaterSource", () => WindFx.DebugFlags.ShowWaterSource, v => WindFx.DebugFlags.ShowWaterSource = v);
            WindLabToggle("ShowWaterDestination", () => WindFx.DebugFlags.ShowWaterDestination, v => WindFx.DebugFlags.ShowWaterDestination = v);
            WindLabToggle("ShowWaterPath", () => WindFx.DebugFlags.ShowWaterPath, v => WindFx.DebugFlags.ShowWaterPath = v);
            WindLabToggle("ShowInfectionSource", () => WindFx.DebugFlags.ShowInfectionSource, v => WindFx.DebugFlags.ShowInfectionSource = v);
            WindLabToggle("ShowInfectionDestination", () => WindFx.DebugFlags.ShowInfectionDestination, v => WindFx.DebugFlags.ShowInfectionDestination = v);
            WindLabToggle("ShowInfectionPath", () => WindFx.DebugFlags.ShowInfectionPath, v => WindFx.DebugFlags.ShowInfectionPath = v);
            WindLabToggle("ShowInteractionTiming", () => WindFx.DebugFlags.ShowInteractionTiming, v => WindFx.DebugFlags.ShowInteractionTiming = v);
            WindLabToggle("ShowVisualBudget", () => WindFx.DebugFlags.ShowVisualBudget, v => WindFx.DebugFlags.ShowVisualBudget = v);
            WindLabToggle("label (start, end, length, counts)", () => WindFx.DebugFlags.ShowLabel, v => WindFx.DebugFlags.ShowLabel = v);
            AddAnim("rüzgar debug: all off, every layer back on", "rüzgar debug: hepsi kapalı, tüm katmanlar açık", () =>
            {
                WindFx.DebugFlags.AllOff();
                WindFx.Layers.AllOn();
                animLastLabel = "debug off";
            });
        }

        private void WindLabToggle(string name, Func<bool> get, Action<bool> set)
        {
            AddAnim("rüzgar debug: " + name, "rüzgar debug: " + name, () =>
            {
                set(!get());
                animLastLabel = name + " " + (get() ? "ON" : "off");
            });
        }

        /// <summary>Closing the lab: the debug views go off and the quality goes back.</summary>
        private void ResetWindLab()
        {
            WindFx.DebugFlags.AllOff();
            WindFx.Layers.AllOn();
            WindFx.Level = windDefaultQuality;
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

        /// <summary>The lab's infections ride the way Enfeksiyon's do: each to the first block
        /// downwind of it in the band that is not infected.</summary>
        private static List<WindCarry> WindLabRides(GameBoard board, WindGust gust, List<GridPos> infected)
        {
            var rides = new List<WindCarry>();
            if (gust == null || !gust.Valid)
            {
                return rides;
            }
            var taken = new HashSet<GridPos>(infected);
            foreach (GridPos cell in gust.Cells)
            {
                if (!infected.Contains(cell))
                {
                    continue;
                }
                foreach (GridPos to in gust.Cells)
                {
                    if (gust.IsAhead(cell, to) && board.GetCube(to).HasValue && !taken.Contains(to))
                    {
                        taken.Add(to);
                        rides.Add(new WindCarry
                        {
                            From = cell,
                            To = to,
                            CarrierId = "enfeksiyon",
                            Seed = cell.X * 7919 + cell.Y * 104729 + to.X * 31 + to.Y
                        });
                        break;
                    }
                }
            }
            return rides;
        }

        /// <summary>
        /// A cast on a board of the lab's own, through the power's own rule (RuzgarPower.BlowOn and
        /// SettleOn - the board's real gravity), played by the game's own storm view. With
        /// <paramref name="isolate"/> it runs fast up to that beat, plays it and holds.
        /// </summary>
        private void WindLabCast(string[] rows, float fx, float fy, float tx, float ty, int catchPercent = 100,
            int embers = 2, WindBeat? isolate = null)
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
            var infections = new List<InfectedCell>();
            foreach (GridPos cell in infected)
            {
                infections.Add(new InfectedCell(cell, 1, 3));
            }
            if (infections.Count > 0)
            {
                // the cores that are there before the wind: shown first, so they are standing ones
                boardView.ShowInfections(infections);
            }
            Dictionary<GridPos, WindStormView.Face> faces = CaptureWindFaces(target);
            WindVisuals report = RuzgarPower.BlowOn(board, gust, new SeededRandom(4242), embers, catchPercent);
            report.EventId = 1;
            RuzgarPower.SettleOn(board, report);
            foreach (WindCarry ride in WindLabRides(board, gust, infected))
            {
                gust.Carries.Add(ride);
                infections.Add(new InfectedCell(ride.To, 0, 3));
            }
            boardView.Refresh();
            if (infections.Count > 0)
            {
                boardView.ShowInfections(infections);
            }
            windStorm.Play(boardView, report, faces);
            bool found = !isolate.HasValue || windStorm.Isolate(isolate.Value);
            animLastLabel = "embers " + report.Embers.Count + " (lit " + report.Ignitions.Count + ")  water "
                + report.Pushes.Count + "  carried " + gust.Carries.Count + "  bystanders " + report.Bystanders.Count
                + "  length " + gust.Length.ToString("0.0")
                + (isolate.HasValue ? (found ? "  [" + isolate.Value + "]" : "  [no " + isolate.Value + " in this cast]") : "");
        }

        /// <summary>The storm with only the named layers on - and nothing reacting, so a layer is
        /// judged on its own rather than by what is over it.</summary>
        private void WindLabLayer(string[] rows, bool f = false, bool b = false, bool r = false, bool m = false,
            bool d = false)
        {
            // the layers are set AFTER the board is built: building it resyncs, which puts them all back
            List<GridPos> unused;
            BuildWindLabBoard(rows, out unused);
            WindFx.Layers.Front = f;
            WindFx.Layers.Body = b;
            WindFx.Layers.Ribbons = r;
            WindFx.Layers.Motes = m;
            WindFx.Layers.Distortion = d;
            WindFx.Layers.Reactions = false;
            EnsureWind();
            var target = ActivationTarget.Swipe(new BoardStroke(0f, 3f, 6f, 3.6f));
            WindGust gust = RuzgarPower.GustFor(boardView.Board, target);
            var report = new WindVisuals(gust) { EventId = 1, Seed = RuzgarPower.SeedOf(gust, 1) };
            windStorm.Play(boardView, report, null);
            animLastLabel = (f ? "front " : "") + (b ? "body " : "") + (r ? "ribbons " : "") + (m ? "particles " : "")
                + (d ? "distortion " : "") + "only";
        }

        private void WindLabQuality(WindFx.Quality level)
        {
            WindFx.Level = level;
            WindLabCast(WindLabDense, 0f, 5f, 6f, 1.5f, embers: 3);
            animLastLabel = "quality " + level + "  " + animLastLabel;
        }

        private enum WindLabAimMode
        {
            Select,
            Cursor,
            Pin,
            Aim,
            Rotate,
            Refused
        }

        /// <summary>The aim, held up over a lab board with no input - so it can be looked at.</summary>
        private void WindLabAim(string[] rows, float fx, float fy, float tx, float ty, WindLabAimMode mode)
        {
            List<GridPos> infected;
            GameBoard board = BuildWindLabBoard(rows, out infected);
            EnsureWind();
            if (infected.Count > 0)
            {
                var infections = new List<InfectedCell>();
                foreach (GridPos cell in infected)
                {
                    infections.Add(new InfectedCell(cell, 1, 3));
                }
                boardView.ShowInfections(infections);
            }
            var start = new GridPos(Mathf.RoundToInt(fx), Mathf.RoundToInt(fy));
            switch (mode)
            {
                case WindLabAimMode.Select:
                    SetWindMood(true);
                    if (sfx != null)
                    {
                        sfx.WindBed(1f, 0f);
                    }
                    windAim.Cursor(boardView, boardView.BoardPointToWorld(fx, fy));
                    animLastLabel = "aim mode: backdrop -3%, the bed, the knot";
                    windLabRoutine = StartCoroutine(WindLabAfter(3f, delegate
                    {
                        SetWindMood(false);
                        if (sfx != null)
                        {
                            sfx.WindBed(0f, 0f);
                        }
                    }));
                    return;
                case WindLabAimMode.Cursor:
                    windAim.Cursor(boardView, boardView.BoardPointToWorld(fx, fy));
                    animLastLabel = "the pressure knot";
                    return;
                case WindLabAimMode.Pin:
                    windAim.Pin(boardView, start);
                    if (sfx != null)
                    {
                        sfx.Wind(WindCue.OriginLock, 0f);
                    }
                    animLastLabel = "origin locked on (" + start.X + "," + start.Y + ")";
                    return;
                case WindLabAimMode.Rotate:
                    windLabRoutine = StartCoroutine(WindLabRotate(board, infected, fx, fy));
                    animLastLabel = "the endpoint swings round the origin";
                    return;
            }
            WindPreview preview = WindLabPreview(board, infected, fx, fy, tx, ty);
            windAim.Aim(boardView, preview, preview.Valid);
            if (mode == WindLabAimMode.Refused)
            {
                windAim.Refuse(Loc.Pick("nothing here to carry", "taşıyacak bir şey yok"));
                if (sfx != null)
                {
                    sfx.Wind(WindCue.Refused, 0f);
                }
            }
            WindGust gust = preview.Gust;
            animLastLabel = "valid " + preview.Valid + "  reason " + preview.Reason + "  affected " + preview.Affected.Count
                + "  length " + (gust != null ? gust.Length.ToString("0.0") : "-")
                + (gust != null && gust.Clamped ? "  (cut to the cap)" : "");
        }

        private static WindPreview WindLabPreview(GameBoard board, List<GridPos> infected, float fx, float fy, float tx,
            float ty)
        {
            WindGust gust = WindGust.From(board, new BoardStroke(fx, fy, tx, ty));
            return RuzgarPower.PreviewOn(board, gust, WindLabRides(board, gust, infected));
        }

        private IEnumerator WindLabRotate(GameBoard board, List<GridPos> infected, float fx, float fy)
        {
            float t = 0f;
            while (t < 8f)
            {
                t += Time.unscaledDeltaTime;
                float angle = t * 0.9f;
                float reach = 2.2f + 1.6f * (0.5f + 0.5f * Mathf.Sin(t * 0.7f));
                WindPreview preview = WindLabPreview(board, infected, fx, fy, fx + Mathf.Cos(angle) * reach,
                    fy + Mathf.Sin(angle) * reach);
                windAim.Aim(boardView, preview, preview.Valid);
                yield return null;
            }
            windLabRoutine = null;
        }

        private IEnumerator WindLabAfter(float seconds, Action then)
        {
            yield return new WaitForSecondsRealtime(seconds);
            then();
            windLabRoutine = null;
        }
    }
}
