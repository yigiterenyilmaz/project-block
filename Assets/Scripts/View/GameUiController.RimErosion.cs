// PURPOSE: The rim erosion's animation, wired up - the one place RimErosionView is reached from,
// by the game and by the animation lab alike.
//
// TWO HALVES, EITHER SIDE OF THE REBUILD. The erosion replaces the board object, and the repaint
// that notices throws the old arena away (BoardView.Rebuild destroys the grid and the surface
// repaints its plate). So the old arena is TAKEN just before that rebuild - its transform, its
// plate, the cells the report says were lost - and the animation is started right after it, on
// the same frame, over the board that has just been built. Both happen inside RefreshAll, so
// every path that can erode (a placement, a hand redraw) is covered by the one seam.
//
// Matched by IDENTITY (RoundEngine.LastRimErosion is a new object per erosion), never by the
// erosion count: a repaint hands back the same object, a loaded round has none.

using System.Collections;
using System.Collections.Generic;
using ProjectBlock.Core;
using UnityEngine;

namespace ProjectBlock.View
{
    partial class GameUiController
    {
        private RimErosionView rimErosion;

        /// <summary>The last erosion played (or passed over), by reference.</summary>
        private RimErosionVisuals rimErosionSeen;

        private RimErosionView RimErosionFx
        {
            get
            {
                if (rimErosion == null)
                {
                    var go = new GameObject("RimErosion");
                    go.transform.SetParent(transform, false);
                    rimErosion = go.AddComponent<RimErosionView>();
                    rimErosion.Sfx = sfx;
                }
                return rimErosion;
            }
        }

        /// <summary>True while the rim is coming off: placement is locked, because until the
        /// refit lands a cell is not where the pointer says it is.</summary>
        private bool RimErosionPlaying
        {
            get { return rimErosion != null && rimErosion.Playing; }
        }

        /// <summary>
        /// BEFORE the rebuild: when the round's rim has just eroded and the view still shows the
        /// arena it eroded from, takes that arena for the animation. Null when there is nothing
        /// new to play - and then nothing was taken.
        /// </summary>
        private RimErosionView.Capture TakeRimErosion(RoundEngine round)
        {
            RimErosionVisuals report = round != null ? round.LastRimErosion : null;
            if (report == null || ReferenceEquals(report, rimErosionSeen))
            {
                return null;
            }
            rimErosionSeen = report;
            if (boardView == null || boardView.Board == null || boardView.Board == round.Board)
            {
                return null;
            }
            return RimErosionFx.Take(boardView, report);
        }

        /// <summary>AFTER the rebuild, on the same frame.</summary>
        private void PlayRimErosion(RimErosionView.Capture shot, Vector2 centre)
        {
            if (shot != null)
            {
                RimErosionFx.Play(boardView, shot, centre);
            }
        }

        // ---- animation lab ----------------------------------------------------------------
        //
        // A board of the lab's own, half full, and the step the RULES take: odd steps lose the top
        // row and the right column, even steps the bottom row and the left one (RoundEngine.
        // ErodeRim). What is fabricated is only the report - the arena before and after and the
        // cubes the bands held - and it is played through the same Take / Play the game uses.

        private Coroutine animRim;

        private void StopAnimRim()
        {
            if (animRim != null)
            {
                StopCoroutine(animRim);
                animRim = null;
            }
            if (rimErosion != null)
            {
                rimErosion.Stop();
            }
        }

        /// <summary>Plays <paramref name="steps"/> erosion steps at once, the first of them step
        /// number <paramref name="firstStep"/>, on a lab board <paramref name="fill"/> percent full.
        /// </summary>
        private void AnimRimErosion(int firstStep, int steps, int fill)
        {
            StopAnimRim();
            animRim = StartCoroutine(AnimRimRoutine(firstStep, steps, fill));
        }

        private IEnumerator AnimRimRoutine(int firstStep, int steps, int fill)
        {
            RoundEngine round = session != null ? session.CurrentRound : null;
            if (round == null || boardView == null)
            {
                animRim = null;
                yield break;
            }
            int w = Mathf.Max(5, round.Board.Width);
            int h = Mathf.Max(5, round.Board.Height);
            List<int> cards = AnimBossCards();
            var before = new GameBoard(w, h);
            for (int x = 0; x < w; x++)
            {
                for (int y = 0; y < h; y++)
                {
                    // In block-sized clumps of one card each, so the rim holds whole pieces.
                    uint roll = AnimHash(x / 2 + 11, y / 2 + 5) % 100u;
                    if (roll < (uint)fill)
                    {
                        int card = cards[(int)(AnimHash(x / 2, y / 2) % (uint)cards.Count)];
                        before.SetCubeAt(new GridPos(x, y), new Cube(CubeKind.Normal, card));
                    }
                }
            }
            boardView.Rebuild(before, MainBoardWorldSize, MainBoardCenter);
            yield return new WaitForSeconds(0.5f);

            int left = 0, right = 0, bottom = 0, top = 0;
            for (int i = 0; i < steps; i++)
            {
                if (((firstStep + i) % 2) == 1)
                {
                    right++;
                    top++;
                }
                else
                {
                    left++;
                    bottom++;
                }
            }
            GameBoard after = GameBoard.CreateResized(before, -left, -right, -bottom, -top);
            if (after == null)
            {
                animRim = null;
                yield break;
            }
            var report = new RimErosionVisuals
            {
                BeforeMinX = before.MinX,
                BeforeMinY = before.MinY,
                BeforeWidth = before.Width,
                BeforeHeight = before.Height,
                AfterMinX = after.MinX,
                AfterMinY = after.MinY,
                AfterWidth = after.Width,
                AfterHeight = after.Height,
                Steps = steps
            };
            for (int x = 0; x < w; x++)
            {
                for (int y = 0; y < h; y++)
                {
                    var cell = new GridPos(x, y);
                    Cube? cube = before.GetCube(cell);
                    if (cube.HasValue && report.WasRemoved(cell))
                    {
                        report.DestroyedCells.Add(cell);
                        report.DestroyedCubes.Add(cube.Value);
                    }
                }
            }
            RimErosionView.Capture shot = RimErosionFx.Take(boardView, report);
            boardView.Rebuild(after, MainBoardWorldSize, MainBoardCenter);
            PlayRimErosion(shot, MainBoardCenter);
            animLastLabel = Loc.Pick(
                "rim erosion: " + w + "x" + h + " -> " + after.Width + "x" + after.Height
                    + ", " + report.DestroyedCells.Count + " cubes went with it",
                "kenar erozyonu: " + w + "x" + h + " -> " + after.Width + "x" + after.Height
                    + ", " + report.DestroyedCells.Count + " küp birlikte gitti");
            if (AnimLabOpen)
            {
                RedrawAnimationLab();
            }
            animRim = null;
        }
    }
}
