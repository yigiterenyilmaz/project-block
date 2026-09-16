// PURPOSE: "Elmas Kazma"'s break, wired up - the one place QuarryBreakView is reached from, by the
// game and by the animation lab alike.
//
// TWO HALVES, ON TWO MOMENTS. The repaint that follows the turn has already emptied the obsidian
// (the rules broke it in the same turn as the sweep), so that repaint is where the stones are
// raised as proxies and HELD - otherwise they would blink out a frame before the sweep had even
// been drawn. The break itself is started from PlayExplosionFeedback, which is when the sweep's
// wave is launched, and it waits for that wave: the charge and the wave's own length plus a short
// settle, read off BoardCleanseView's Style rather than written down here.
//
// Where the stones are, how big a cube is, what a screen pixel is, where the score is and the
// face each stone wore are all asked, never stored.

using System.Collections.Generic;
using ProjectBlock.Core;
using UnityEngine;

namespace ProjectBlock.View
{
    partial class GameUiController
    {
        private QuarryBreakView quarry;

        private ElmasKazmaJoker FindPickaxe()
        {
            if (session == null || session.Jokers == null)
            {
                return null;
            }
            IReadOnlyList<Joker> owned = session.Jokers.Jokers;
            for (int i = 0; i < owned.Count; i++)
            {
                var found = owned[i] as ElmasKazmaJoker;
                if (found != null)
                {
                    return found;
                }
            }
            return null;
        }

        /// <summary>The repaint's half: raise and hold the stones the pickaxe took.</summary>
        private void SyncQuarry()
        {
            ElmasKazmaJoker joker = FindPickaxe();
            if (joker == null || joker.LastQuarry == null || boardView == null)
            {
                return;
            }
            EnsureQuarry();
            quarry.Prepare(joker.LastQuarry);
        }

        /// <summary>The turn's half: the sweep is launched now; the pickaxe follows its wave.</summary>
        private void PlayQuarry(TurnReport report)
        {
            ElmasKazmaJoker joker = FindPickaxe();
            if (joker == null || joker.LastQuarry == null || boardView == null)
            {
                return;
            }
            EnsureQuarry();
            quarry.Begin(joker.LastQuarry, report != null && report.CleanSweep ? QuarryAfterSweep() : QuarryBreath());
        }

        /// <summary>How long the sweep takes to be seen through, plus a breath.</summary>
        private static float QuarryAfterSweep()
        {
            return BoardCleanseView.Style.ChargeUpDuration + BoardCleanseView.Style.WaveSeconds
                + QuarryBreakView.Style.Settle;
        }

        private static float QuarryBreath()
        {
            return QuarryBreakView.Style.Settle;
        }

        private void EnsureQuarry()
        {
            if (quarry != null)
            {
                return;
            }
            var go = new GameObject("QuarryBreak");
            go.transform.SetParent(transform, false);
            quarry = go.AddComponent<QuarryBreakView>();
            quarry.CellWorld = delegate(GridPos cell)
            {
                return (Vector2)boardView.transform.TransformPoint(boardView.CellToWorld(cell));
            };
            quarry.CubeSize = delegate { return boardView.CubeWorldSize * boardView.transform.lossyScale.x; };
            quarry.Pixel = delegate
            {
                return cam != null ? cam.orthographicSize * 2f / Mathf.Max(1, Screen.height) : 0.01f;
            };
            quarry.ScoreAnchor = ScoreWorldAnchor;
            quarry.BoardRect = QuarryBoardRect;
            quarry.Face = delegate(GridPos cell, Cube cube, out Sprite tile, out Color colour)
            {
                // The face the board last showed (a blind round stays blind), else the cube's own.
                if (!boardView.TryCubeLook(cell, 2f, out tile, out colour))
                {
                    boardView.CubeFace(cube, out tile, out colour);
                }
                return tile != null;
            };
        }

        private Rect QuarryBoardRect()
        {
            GameBoard board = boardView != null ? boardView.Board : null;
            if (board == null)
            {
                return new Rect();
            }
            Transform arena = boardView.transform;
            float half = boardView.CellWorldSize * 0.5f;
            Vector2 low = arena.TransformPoint(boardView.CellToWorld(new GridPos(board.MinX, board.MinY))
                - new Vector2(half, half));
            Vector2 high = arena.TransformPoint(boardView.CellToWorld(new GridPos(board.MinX + board.Width - 1,
                board.MinY + board.Height - 1)) + new Vector2(half, half));
            return Rect.MinMaxRect(low.x, low.y, high.x, high.y);
        }

        private void StopQuarry()
        {
            StopAnimQuarryScenario();
            if (quarry != null)
            {
                quarry.Stop();
                ElmasKazmaJoker joker = FindPickaxe();
                quarry.MarkSeen(joker != null ? joker.LastQuarry : null);
            }
        }
    }
}
