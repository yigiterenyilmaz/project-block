// PURPOSE: "Taşkın"'s overflow, wired up - the one place FloodView is reached from, by the game and
// by the animation lab alike.
//
// The joker shares its report with "Yangın" (SpreadVisuals), and FireSpreadView plays only fire; this
// plays only water. It is asked on the repaint that follows the joker's use, exactly where the fire
// spread is asked. The old face comes from the REPORT (SpreadIgnition.Was) and never from the board:
// the board has already been repainted as water by the time this runs, and asking it what a cell
// looks like returns the answer the animation is meant to arrive at.

using System.Collections.Generic;
using ProjectBlock.Core;
using UnityEngine;

namespace ProjectBlock.View
{
    partial class GameUiController
    {
        private FloodView flood;

        private void SyncFlood()
        {
            if (session == null || session.Jokers == null || boardView == null)
            {
                return;
            }
            IReadOnlyList<Joker> owned = session.Jokers.Jokers;
            for (int i = 0; i < owned.Count; i++)
            {
                var spread = owned[i] as SpreadJoker;
                if (spread != null && spread.LastSpread != null && spread.LastSpread.Kind == CubeKind.Water)
                {
                    EnsureFlood();
                    flood.Play(spread.LastSpread);
                    // The water the flood made FALLS once it has turned - the same fall a
                    // placement's water takes, played after the conversion has had time to land.
                    if (!ReferenceEquals(spread.LastSpread, lastFloodFall)
                        && spread.LastSpread.FallFrames.Count > 0)
                    {
                        lastFloodFall = spread.LastSpread;
                        StartCoroutine(PlayFloodFall(spread.LastSpread));
                    }
                }
            }
            SyncSpreadBuildup();
        }

        private SpreadVisuals lastFloodFall;
        private SpreadBuildupView spreadBuildup;

        private System.Collections.IEnumerator PlayFloodFall(SpreadVisuals report)
        {
            yield return new WaitForSeconds(0.85f);
            if (boardView != null)
            {
                waterAnimating = true;
                boardView.PlayWaterAnimation(report.FallFrames, delegate { waterAnimating = false; });
            }
        }

        /// <summary>The turn before a spread goes off, its source cubes build up.</summary>
        private void SyncSpreadBuildup()
        {
            if (boardView == null)
            {
                return;
            }
            if (spreadBuildup == null)
            {
                var go = new GameObject("SpreadBuildup");
                go.transform.SetParent(boardView.transform, false);
                spreadBuildup = go.AddComponent<SpreadBuildupView>();
                spreadBuildup.Build(boardView);
            }
            RoundEngine round = session != null ? session.CurrentRound : null;
            List<GridPos> fire = null;
            List<GridPos> water = null;
            if (round != null && session.Phase == GamePhase.Round && round.Board != null)
            {
                foreach (Joker j in session.Jokers.Jokers)
                {
                    var spread = j as SpreadJoker;
                    if (spread == null || spread.TurnsUntilSpread > 1)
                    {
                        continue;
                    }
                    if (spread.SpreadKind == CubeKind.Fire)
                    {
                        fire = round.Board.CellsOfKind(CubeKind.Fire);
                    }
                    else if (spread.SpreadKind == CubeKind.Water)
                    {
                        water = round.Board.CellsOfKind(CubeKind.Water);
                    }
                }
            }
            spreadBuildup.Show(fire, water);
        }

        private void EnsureFlood()
        {
            if (flood != null)
            {
                return;
            }
            var go = new GameObject("Flood");
            go.transform.SetParent(transform, false);
            flood = go.AddComponent<FloodView>();
            flood.CellWorld = delegate(GridPos cell)
            {
                return (Vector2)boardView.transform.TransformPoint(boardView.CellToWorld(cell));
            };
            flood.CubeSize = delegate { return boardView.CubeWorldSize * boardView.transform.lossyScale.x; };
            flood.CellSize = delegate { return boardView.CellWorldSize * boardView.transform.lossyScale.x; };
            flood.Pixel = delegate
            {
                return cam != null ? cam.orthographicSize * 2f / Mathf.Max(1, Screen.height) : 0.01f;
            };
            flood.Hold = delegate(IEnumerable<GridPos> cells) { boardView.HoldCells(cells); };
            flood.Release = delegate(IEnumerable<GridPos> cells) { boardView.ReleaseCells(cells); };
            flood.Face = delegate(GridPos cell, Cube cube, out Sprite tile, out Color colour)
            {
                boardView.CubeFace(cube, out tile, out colour);
                return tile != null;
            };
        }

        private void StopFlood()
        {
            StopAnimFloodOrdering();
            if (flood != null)
            {
                flood.Stop();
                IReadOnlyList<Joker> owned = session != null && session.Jokers != null ? session.Jokers.Jokers : null;
                if (owned != null)
                {
                    foreach (Joker j in owned)
                    {
                        var spread = j as SpreadJoker;
                        if (spread != null && spread.LastSpread != null && spread.LastSpread.Kind == CubeKind.Water)
                        {
                            flood.MarkPlayed(spread.LastSpread);
                        }
                    }
                }
            }
        }
    }
}
