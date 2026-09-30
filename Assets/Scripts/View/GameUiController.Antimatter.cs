// PURPOSE: "Antimadde" wired up - the one place AntimatterBlastView is reached from, by the game
// and by the animation lab alike.
//
// TWO HALVES, ON TWO MOMENTS (the "Elmas Kazma" bargain). The rules emptied the annihilated cells
// in the turn itself, so the repaint that follows has already taken the cubes away; that repaint
// is where they are raised as PROXIES and held (SyncAntimatter, from RefreshAll), or they would
// blink out before anything was drawn. The event itself starts from PlayExplosionFeedback, which
// is when the turn's destruction is shown (PlayAntimatter).
//
// THE ANNIHILATED CELLS DO NOT GO THROUGH THE CLUSTER BURST. Matter turning into energy throws no
// debris, so EmitBlastParticles leaves them out of FlashCells (WithoutAnnihilated) and the view is
// the only thing that draws them.
//
// A CLEAN SWEEP THE ANNIHILATION CAUSED WAITS FOR IT: annihilation, the cells gone, a breath, and
// only then the sweep - never two huge events at once. PlayExplosionFeedback decides that once
// (sweepWaitsForAntimatter) and the sweep's sound, shake, popup and wave are played from here
// after AntimatterBlastView.Style.CleanupDelay.
//
// The View decides nothing: the kind and cells are the TurnReport's, the payment the joker's
// measured report (AntimaddeJoker.LastAnnihilation, matched by identity AND by its cells, so a
// stale one never lends its number to a key it did not pay for), and the faces are what the board
// last showed.

using System.Collections;
using System.Collections.Generic;
using ProjectBlock.Core;
using UnityEngine;

namespace ProjectBlock.View
{
    partial class GameUiController
    {
        private AntimatterBlastView antimatter;

        /// <summary>The report the current request was built for, and the request.</summary>
        private TurnReport antimatterRequestKey;
        private AntimatterBlastView.Request antimatterRequest;

        /// <summary>The joker report last lent to an event - a report is used once.</summary>
        private AntimatterVisuals antimatterPaidLast;

        /// <summary>True for the feedback pass whose clean sweep waits for the annihilation.</summary>
        private bool sweepWaitsForAntimatter;

        /// <summary>True if this turn's report was an annihilation - the plain explode sound is
        /// then left to the event's own.</summary>
        private static bool IsAnnihilation(TurnReport report)
        {
            return report != null && report.AnnihilatedKind.HasValue;
        }

        /// <summary>The repaint's half: raise and hold the cubes the key annihilated.</summary>
        private void SyncAntimatter(TurnReport report)
        {
            if (!IsAnnihilation(report) || boardView == null)
            {
                return;
            }
            EnsureAntimatter();
            if (antimatter.HasSeen(report))
            {
                return;
            }
            antimatter.Prepare(report, AntimatterRequestFor(report));
        }

        /// <summary>The turn's half: the destruction is being shown now, and so is the event.</summary>
        private void PlayAntimatter(TurnReport report)
        {
            if (!IsAnnihilation(report) || boardView == null)
            {
                return;
            }
            EnsureAntimatter();
            antimatter.Begin(report, antimatter.HasSeen(report) ? null : AntimatterRequestFor(report));
        }

        /// <summary>Called once the turn's sweep is known to be an ordinary one: an annihilation
        /// that emptied the board has its sweep wait until the event has been seen through.</summary>
        private bool DeferSweepForAntimatter(RoundEngine round, TurnReport report)
        {
            if (!IsAnnihilation(report) || !report.CleanSweep || sweepIsHoleCollapse)
            {
                return false;
            }
            StartCoroutine(PlayDeferredSweep(round, AntimatterBlastView.Style.CleanupDelay));
            return true;
        }

        private IEnumerator PlayDeferredSweep(RoundEngine round, float delay)
        {
            yield return new WaitForSeconds(delay);
            int sweeps = round != null ? round.CleanSweepCount : 1;
            sfx.CleanSweep(1f + 0.12f * Mathf.Min(sweeps - 1, 8));
            sfx.Flame();
            ShakeForBlast(false, true, Mathf.Max(1, comboStreak));
            SpawnSweepPopup();
            EmitSweepConfetti();
        }

        /// <summary>The cells to hand the cluster burst: every one of them but the annihilated,
        /// which the event draws itself.</summary>
        private IReadOnlyList<GridPos> WithoutAnnihilated(TurnReport report, IReadOnlyList<GridPos> cells)
        {
            if (!IsAnnihilation(report) || cells == null || cells.Count == 0)
            {
                return cells;
            }
            AntimatterBlastView.Request request = AntimatterRequestFor(report);
            var taken = new HashSet<GridPos>();
            foreach (AntimatterBlastView.Target t in request.Targets)
            {
                taken.Add(t.Cell);
            }
            var left = new List<GridPos>(cells.Count);
            foreach (GridPos cell in cells)
            {
                if (!taken.Contains(cell))
                {
                    left.Add(cell);
                }
            }
            return left;
        }

        private AntimatterBlastView.Request AntimatterRequestFor(TurnReport report)
        {
            if (!ReferenceEquals(report, antimatterRequestKey) || antimatterRequest == null)
            {
                antimatterRequestKey = report;
                antimatterRequest = BuildAntimatterRequest(report);
            }
            return antimatterRequest;
        }

        private AntimatterBlastView.Request BuildAntimatterRequest(TurnReport report)
        {
            CubeKind kind = report.AnnihilatedKind.Value;
            var request = new AntimatterBlastView.Request { Kind = kind };
            var cells = new List<GridPos>();
            var cubes = new List<Cube>();
            AntimatterVisuals paid = FreshAnnihilation(report);
            if (paid != null)
            {
                cells.AddRange(paid.Cells);
                cubes.AddRange(paid.Cubes);
                request.Points = paid.Points;
                request.PointsPerCube = paid.PointsPerCube;
                request.Seed = paid.Seed;
            }
            else
            {
                // No joker paid for this one (it was sold, or the key came from somewhere else):
                // the blast is the engine's and still plays; there is simply no number to show.
                var billed = new HashSet<GridPos>(report.ExtraExplodedCells);
                uint seed = 2166136261u;
                foreach (DestroyedCube dead in report.DestroyedCubes)
                {
                    if (dead.Cube.Kind != kind || !billed.Contains(dead.Pos) || cells.Contains(dead.Pos))
                    {
                        continue;
                    }
                    cells.Add(dead.Pos);
                    cubes.Add(dead.Cube);
                    unchecked
                    {
                        seed = (seed ^ (uint)(dead.Pos.X * 73856093)) * 16777619u;
                        seed = (seed ^ (uint)(dead.Pos.Y * 19349663)) * 16777619u;
                    }
                }
                request.Seed = seed;
            }
            for (int i = 0; i < cells.Count; i++)
            {
                request.Targets.Add(AntimatterTarget(cells[i], i < cubes.Count ? cubes[i] : new Cube(kind, -1)));
            }
            return request;
        }

        /// <summary>A cell and the cube that stood there, dressed in the face the board last showed
        /// (a blind round stays blind), else the cube's own.</summary>
        private AntimatterBlastView.Target AntimatterTarget(GridPos cell, Cube cube)
        {
            Sprite tile;
            Color tint;
            if (!boardView.TryCubeLook(cell, 5f, out tile, out tint))
            {
                boardView.CubeFace(cube, out tile, out tint);
            }
            return new AntimatterBlastView.Target
            {
                Cell = cell,
                Tile = tile,
                Tint = tint,
                Matter = ViewUtil.CubeMaterialColor(cube)
            };
        }

        /// <summary>The joker's report for THIS key: new since the last one used, of the same kind,
        /// and naming the same cells the turn emptied.</summary>
        private AntimatterVisuals FreshAnnihilation(TurnReport report)
        {
            if (session == null || session.Jokers == null)
            {
                return null;
            }
            IReadOnlyList<Joker> owned = session.Jokers.Jokers;
            for (int i = 0; i < owned.Count; i++)
            {
                var joker = owned[i] as AntimaddeJoker;
                AntimatterVisuals seen = joker != null ? joker.LastAnnihilation : null;
                // The joker bills ExtraExplodedCells as they stand when it pays; a late clear later
                // in the turn can only add to the end of that list, so the report is its prefix.
                if (seen == null || ReferenceEquals(seen, antimatterPaidLast)
                    || seen.Kind != report.AnnihilatedKind.Value
                    || seen.Count == 0 || seen.Count > report.ExtraExplodedCells.Count)
                {
                    continue;
                }
                bool same = true;
                for (int c = 0; c < seen.Count && same; c++)
                {
                    same = seen.Cells[c].Equals(report.ExtraExplodedCells[c]);
                }
                if (same)
                {
                    antimatterPaidLast = seen;
                    return seen;
                }
            }
            return null;
        }

        private void EnsureAntimatter()
        {
            if (antimatter != null)
            {
                return;
            }
            // NOT under the board: BoardView.Rebuild destroys the board's children, and the lab
            // rebuilds a board of its own right before it plays. The view follows the board's
            // transform instead, so the overtime squeeze and the impulse still carry it.
            var go = new GameObject("AntimatterBlast");
            go.transform.SetParent(transform, false);
            antimatter = go.AddComponent<AntimatterBlastView>();
            antimatter.Arena = delegate { return boardView != null ? boardView.transform : null; };
            antimatter.CellLocal = delegate(GridPos cell) { return boardView.CellToWorld(cell); };
            antimatter.CellSize = delegate { return boardView.CellWorldSize; };
            antimatter.CubeSize = delegate { return boardView.CubeWorldSize; };
            antimatter.PixelWorld = delegate
            {
                return cam != null ? cam.orthographicSize * 2f / Mathf.Max(1, Screen.height) : 0.01f;
            };
            antimatter.BoardLocal = delegate { return boardView.WorldRect; };
            antimatter.Bystanders = AntimatterBystanders;
            antimatter.ScreenRect = CameraWorldRect;
            antimatter.ScoreAnchor = ScoreWorldAnchor;
            antimatter.Sounded = PlayAntimatterSound;
            antimatter.Mood = SetAntimatterMood;
            antimatter.Impulse = delegate(Vector2 offset)
            {
                if (boardView != null)
                {
                    boardView.SetImpulse(offset);
                }
            };
            antimatter.Build();
        }

        /// <summary>The cubes still standing, which the shockwave only LIGHTS as it passes. None on
        /// a blind board: a rim lighting up in the dark would give away a cube the dark hid.</summary>
        private void AntimatterBystanders(List<Vector2> into)
        {
            GameBoard board = boardView != null ? boardView.Board : null;
            if (board == null || boardView.IsDark)
            {
                return;
            }
            foreach (GridPos cell in board.GetOccupiedCells())
            {
                into.Add(boardView.CellToWorld(cell));
            }
        }

        /// <summary>The event's moments, as sound. The implosion's clip ends short of the peak on
        /// purpose - that silence is what the peak lands in.</summary>
        private void PlayAntimatterSound(AntimatterBlastView.Beat beat)
        {
            switch (beat)
            {
                case AntimatterBlastView.Beat.RealityDistort: sfx.AntimatterDistort(); break;
                case AntimatterBlastView.Beat.Ghost: sfx.AntimatterGhost(); break;
                case AntimatterBlastView.Beat.Charge: sfx.AntimatterCharge(); break;
                case AntimatterBlastView.Beat.Contact: sfx.AntimatterContact(); break;
                case AntimatterBlastView.Beat.Implosion: sfx.AntimatterImplosion(); break;
                case AntimatterBlastView.Beat.Peak: sfx.AntimatterPeak(); break;
                case AntimatterBlastView.Beat.Beam: sfx.AntimatterBeam(); break;
                case AntimatterBlastView.Beat.Shockwave: sfx.AntimatterShockwave(); break;
                case AntimatterBlastView.Beat.Aftermath: sfx.AntimatterAftermath(); break;
                case AntimatterBlastView.Beat.Reward: sfx.AntimatterReward(); break;
            }
        }

        /// <summary>The background's answer: a few percent darker and greyer through the buildup
        /// so the energy reads brighter, its warmth taken out and a little violet let in at the
        /// peak, then handed back. Never a wash - the multipliers stay within a few percent.</summary>
        private void SetAntimatterMood(float dim, float violet)
        {
            if (background == null)
            {
                return;
            }
            if (dim <= 0.0001f && violet <= 0.0001f)
            {
                background.SetMood("antimatter", null);
                return;
            }
            GameBackgroundPresentationController.Mood mood = GameBackgroundPresentationController.Mood.Neutral;
            mood.Brightness = 1f - 0.05f * dim;
            mood.Saturation = 1f - 0.045f * dim;
            mood.Warmth = Mathf.Clamp01(1f - violet);
            mood.PlumStrength = 1f + 0.35f * violet;
            background.SetMood("antimatter", mood);
        }

        /// <summary>Puts the event away and marks the turn's report as seen - the lab's reset.</summary>
        private void StopAntimatter()
        {
            StopAnimAntimatter();
            if (antimatter != null)
            {
                antimatter.Stop();
                antimatter.MarkSeen(antimatterRequestKey);
            }
            // Whatever a lab scene switched off, froze or downgraded goes back, so the real game
            // never plays a lab's leftovers.
            AntimatterBlastView.Style.HoldAt = -1f;
            AntimatterBlastView.Style.GhostApart = 0f;
            AntimatterBlastView.Style.Level = Application.isMobilePlatform
                ? AntimatterBlastView.Quality.Medium : AntimatterBlastView.Quality.High;
            AntimatterBlastView.Layers.AllOn();
            AntimatterBlastView.Layers.DebugOff();
        }
    }
}
