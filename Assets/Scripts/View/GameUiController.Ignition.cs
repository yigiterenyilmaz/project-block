// PURPOSE: "Tutuştur"'s combustion wave, wired up - the one place IgnitionBurnView is reached from,
// by the game and by the animation lab alike.
//
// TWO HALVES, ON TWO MOMENTS, the same bargain as "Elmas Kazma": the repaint that follows the turn
// has already emptied every fire the chain took, so that repaint raises them as proxies and HOLDS
// them (SyncIgnition -> Prepare); the wave is started from PlayExplosionFeedback, which is when the
// source fire's own explosion is drawn, and it begins at that explosion's PEAK - the line sweep's
// propagation and hold, read off LineSweepView.Style rather than written down here.

using System.Collections.Generic;
using ProjectBlock.Core;
using UnityEngine;

namespace ProjectBlock.View
{
    partial class GameUiController
    {
        private IgnitionBurnView ignition;

        private TutusturJoker FindIgniter()
        {
            if (session == null || session.Jokers == null)
            {
                return null;
            }
            IReadOnlyList<Joker> owned = session.Jokers.Jokers;
            for (int i = 0; i < owned.Count; i++)
            {
                var found = owned[i] as TutusturJoker;
                if (found != null)
                {
                    return found;
                }
            }
            return null;
        }

        private void SyncIgnition()
        {
            TutusturJoker joker = FindIgniter();
            if (joker == null || joker.LastIgnition == null || boardView == null)
            {
                return;
            }
            EnsureIgnition();
            ignition.Prepare(joker.LastIgnition);
        }

        private void PlayIgnition()
        {
            TutusturJoker joker = FindIgniter();
            if (joker == null || joker.LastIgnition == null || boardView == null)
            {
                return;
            }
            EnsureIgnition();
            ignition.Begin(joker.LastIgnition, IgnitionPeak());
        }

        /// <summary>When the source's explosion peaks: the line sweep's travel and hold.</summary>
        private static float IgnitionPeak()
        {
            return LineSweepView.Style.PropagationSeconds + LineSweepView.Style.PeakHold;
        }

        private void EnsureIgnition()
        {
            if (ignition != null)
            {
                return;
            }
            var go = new GameObject("IgnitionBurn");
            go.transform.SetParent(transform, false);
            ignition = go.AddComponent<IgnitionBurnView>();
            ignition.CellWorld = delegate(GridPos cell)
            {
                return (Vector2)boardView.transform.TransformPoint(boardView.CellToWorld(cell));
            };
            ignition.CubeSize = delegate { return boardView.CubeWorldSize * boardView.transform.lossyScale.x; };
            ignition.CellSize = delegate { return boardView.CellWorldSize * boardView.transform.lossyScale.x; };
            ignition.Pixel = delegate
            {
                return cam != null ? cam.orthographicSize * 2f / Mathf.Max(1, Screen.height) : 0.01f;
            };
            ignition.ScoreAnchor = ScoreWorldAnchor;
            ignition.BoardRect = QuarryBoardRect;
            ignition.RowOf = delegate(GridPos cell)
            {
                GameBoard board = boardView.Board;
                return board != null ? cell.Y - board.MinY : cell.Y;
            };
            ignition.Face = delegate(GridPos cell, Cube cube, out Sprite tile, out Color colour)
            {
                if (!boardView.TryCubeLook(cell, 2f, out tile, out colour))
                {
                    boardView.CubeFace(cube, out tile, out colour);
                }
                return tile != null;
            };
        }

        private void StopIgnition()
        {
            StopAnimIgnitionScenario();
            if (ignition != null)
            {
                ignition.Stop();
                TutusturJoker joker = FindIgniter();
                ignition.MarkSeen(joker != null ? joker.LastIgnition : null);
            }
        }
    }
}
