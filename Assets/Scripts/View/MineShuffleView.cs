// PURPOSE: The shell game on screen ("Mayın eşeği"). It covers the whole arena in black tiles,
// marks the one hiding the mine, dances that cover across the board, and lifts everything again.
//
// IT ANIMATES WHAT THE RULES DID - nothing else. The path comes from the boss
// (MayinEsegiBoss.ShufflePath), which computed it off the round's own rng, so the cover the player
// follows really is where the mine went. A View that made up its own dance would be lying to them.
//
// ONCE THE COVER IS DOWN, THE MINE IS NOT VISIBLE. That is the entire game here, and it was the
// bug: the marker used to be re-sorted ON TOP of the covers for the whole dance, so a player simply
// watched a red square move and there was nothing to lose track of. What you follow is the COVER -
// you are shown which one it is as it lands, and after that every cover on the board is identical.
//
// The cubes underneath are untouched and unmoved throughout; only the covers move. Placeholder
// presentation like everything else under View/.

using System.Collections;
using System.Collections.Generic;
using ProjectBlock.Core;
using UnityEngine;

namespace ProjectBlock.View
{
    /// <summary>The cover-the-board-and-shuffle animation.</summary>
    public sealed class MineShuffleView : MonoBehaviour
    {
        /// <summary>How long the mine sits revealed before the covers come down.</summary>
        private const float RevealSeconds = 1.1f;

        /// <summary>One hop of the dance. Slow enough to be followable, fast enough to be a dance.
        /// </summary>
        private const float HopSeconds = 0.26f;

        /// <summary>How long the board stays covered after the last hop, before the lift.</summary>
        private const float SettleSeconds = 0.45f;

        /// <summary>The pause after the other covers land and before the mine's does - what makes
        /// the last one arriving a beat the eye can catch rather than part of a wave.</summary>
        private const float LandBeatSeconds = 0.28f;

        /// <summary>The mine's cover dropping into place.</summary>
        private const float LandSeconds = 0.22f;

        private static readonly Color CoverColor = new Color(0.06f, 0.06f, 0.09f);
        private static readonly Color MineColor = new Color(0.85f, 0.22f, 0.18f);

        /// <summary>True while the dance is running. The controller blocks play meanwhile - a
        /// player cannot be asked to watch and act at the same time.</summary>
        public bool IsRunning { get; private set; }

        private readonly Dictionary<GridPos, SpriteRenderer> covers =
            new Dictionary<GridPos, SpriteRenderer>();
        private SpriteRenderer mineMarker;
        private Coroutine running;

        /// <summary>Runs the whole reveal-cover-shuffle-lift on the given path.</summary>
        public void Play(BoardView board, GameBoard model, IReadOnlyList<GridPos> path)
        {
            Stop();
            if (board == null || model == null || path == null || path.Count == 0)
            {
                return;
            }
            running = StartCoroutine(Dance(board, model, new List<GridPos>(path)));
        }

        public void Stop()
        {
            if (running != null)
            {
                StopCoroutine(running);
                running = null;
            }
            IsRunning = false;
            covers.Clear();
            mineMarker = null;
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Destroy(transform.GetChild(i).gameObject);
            }
        }

        private IEnumerator Dance(BoardView board, GameBoard model, List<GridPos> path)
        {
            IsRunning = true;
            float size = board.CellWorldSize;

            // 1. The reveal: the mine's cell is marked while the board is still plainly visible.
            GridPos at = path[0];
            mineMarker = ViewUtil.MakeCell(transform, "Mine", board.CellToWorld(at),
                size * 0.86f, MineColor, 30);
            yield return new WaitForSeconds(RevealSeconds);

            // 2. The covers come down over everything EXCEPT the mine's cell, which is left
            //    showing for a beat longer. That beat is the whole contract with the player: they
            //    are told exactly which cover to follow, once, and then they are on their own.
            for (int x = model.MinX; x < model.MinX + model.Width; x++)
            {
                for (int y = model.MinY; y < model.MinY + model.Height; y++)
                {
                    var cell = new GridPos(x, y);
                    if (!model.IsInside(cell) || (cell.X == at.X && cell.Y == at.Y))
                    {
                        continue;
                    }
                    covers[cell] = ViewUtil.MakeCell(transform, "Cover_" + x + "_" + y,
                        board.CellToWorld(cell), size * 0.92f, CoverColor, 29);
                }
            }
            yield return new WaitForSeconds(LandBeatSeconds);

            // 3. And now the last cover lands on it. It drops from slightly above and settles, so
            //    the eye has something to lock onto - after this it is one of the crowd.
            SpriteRenderer mineCover = ViewUtil.MakeCell(transform, "Cover_Mine",
                board.CellToWorld(at), size * 0.92f, CoverColor, 29);
            covers[at] = mineCover;
            Vector2 rest = board.CellToWorld(at);
            float drop = 0f;
            while (drop < LandSeconds)
            {
                drop += Time.deltaTime;
                float k = Mathf.Clamp01(drop / LandSeconds);
                float above = (1f - k) * (1f - k) * size * 0.55f;
                mineCover.transform.localPosition =
                    (Vector3)rest + new Vector3(0f, above, 0f);
                yield return null;
            }
            mineCover.transform.localPosition = rest;

            // THE MINE IS NOW HIDDEN. It keeps travelling with its cover below, so the marker is
            // where it should be when everything is torn down - but nothing shows it again. A
            // player who lost the cover is guessing, which is what this boss is for.
            mineMarker.enabled = false;
            yield return new WaitForSeconds(0.15f);

            // 4. The dance. Every hop swaps the mine's cover with the one it is moving to, so
            //    the board of covers stays a board of covers and the only thing telling them
            //    apart is the movement itself.
            for (int step = 1; step < path.Count; step++)
            {
                GridPos from = at;
                GridPos to = path[step];
                yield return Hop(board, from, to);
                at = to;
            }

            yield return new WaitForSeconds(SettleSeconds);
            Stop(); // the covers lift and the cubes are exactly where they always were
        }

        /// <summary>Slides the mine's marker and swaps the two covers under it.</summary>
        private IEnumerator Hop(BoardView board, GridPos from, GridPos to)
        {
            Vector2 a = board.CellToWorld(from);
            Vector2 b = board.CellToWorld(to);
            SpriteRenderer coverA;
            SpriteRenderer coverB;
            covers.TryGetValue(from, out coverA);
            covers.TryGetValue(to, out coverB);

            float t = 0f;
            while (t < HopSeconds)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / HopSeconds);
                // A little arc, so two covers crossing read as passing each other rather than
                // sliding through one another.
                float lift = Mathf.Sin(k * Mathf.PI) * 0.22f;
                if (mineMarker != null)
                {
                    mineMarker.transform.localPosition =
                        (Vector3)Vector2.Lerp(a, b, k) + new Vector3(0f, lift, 0f);
                }
                if (coverA != null)
                {
                    coverA.transform.localPosition =
                        (Vector3)Vector2.Lerp(a, b, k) + new Vector3(0f, lift, 0f);
                }
                if (coverB != null)
                {
                    coverB.transform.localPosition =
                        (Vector3)Vector2.Lerp(b, a, k) - new Vector3(0f, lift, 0f);
                }
                yield return null;
            }
            // The two covers have changed places, so the map has to agree.
            if (coverA != null)
            {
                covers[to] = coverA;
            }
            if (coverB != null)
            {
                covers[from] = coverB;
            }
        }
    }
}
