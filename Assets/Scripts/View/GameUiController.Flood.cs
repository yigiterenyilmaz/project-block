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
                }
            }
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
