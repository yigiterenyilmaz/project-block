// PURPOSE: The shell game on screen ("Mayın eşeği"). It blackens the mine's own cube, covers the
// whole arena in that same black, dances the mine's cover across the board, and lifts everything
// again.
//
// THE REVEAL IS RED, THE DANCE IS NOT. The mine is named in red while the board is still readable
// and the red goes out the moment the covers land: from there its cover is one of many identical
// covers, and following it means following the MOTION, the way you would follow a cup on a table.
// A red badge riding the dance made it a formality - you cannot lose a red square.
//
// IT ANIMATES WHAT THE RULES DID - nothing else. The path comes from the boss
// (MayinEsegiBoss.ShufflePath), which computed it off the round's own rng, so the cover the player
// follows really is where the mine went. A View that made up its own dance would be lying to them.
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
        /// <summary>How long the mine's cube sits BLACK on an otherwise ordinary board, before
        /// the marker names it. This is the beat that says "this cube, that one there".</summary>
        private const float BlackenSeconds = 0.55f;

        /// <summary>How long the mine sits revealed before the covers come down.</summary>
        private const float RevealSeconds = 1.1f;

        /// <summary>One hop of the dance. Slow enough to be followable, fast enough to be a dance.
        /// </summary>
        private const float HopSeconds = 0.26f;

        /// <summary>How long the board stays covered after the last hop, before the lift.</summary>
        private const float SettleSeconds = 0.45f;

        /// <summary>The covers, and the black the mine's own cube goes during the reveal.</summary>
        public static readonly Color CoverColor = new Color(0.06f, 0.06f, 0.09f);

        /// <summary>The mine's red. Public because the DETONATION is drawn elsewhere (see
        /// GameUiController.Feedback) and the blast and the marker have to be the same red, or
        /// the thing that went off does not read as the thing you were following.</summary>
        public static readonly Color MineColor = new Color(0.85f, 0.22f, 0.18f);

        /// <summary>True while the dance is running. The controller blocks play meanwhile - a
        /// player cannot be asked to watch and act at the same time.</summary>
        public bool IsRunning { get; private set; }

        private readonly Dictionary<GridPos, SpriteRenderer> covers =
            new Dictionary<GridPos, SpriteRenderer>();
        private SpriteRenderer mineMarker;
        private Coroutine running;

        /// <summary>Runs the whole reveal-cover-shuffle-lift on the given path.
        /// <paramref name="delaySeconds"/> holds it back before the first beat, which is what a
        /// DETONATION needs: setting the mine off arms a fresh one at once, so the blast and the
        /// new mine's reveal would otherwise land on the same frame and neither would read.
        /// </summary>
        public void Play(BoardView board, GameBoard model, IReadOnlyList<GridPos> path,
            float delaySeconds = 0f)
        {
            Stop();
            if (board == null || model == null || path == null || path.Count == 0)
            {
                return;
            }
            running = StartCoroutine(Dance(board, model, new List<GridPos>(path), delaySeconds));
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

        private IEnumerator Dance(BoardView board, GameBoard model, List<GridPos> path,
            float delaySeconds)
        {
            IsRunning = true;
            if (delaySeconds > 0f)
            {
                yield return new WaitForSeconds(delaySeconds);
            }
            float size = board.CellWorldSize;

            // 1. The reveal, in two beats, and the order is the point. FIRST the mine's own cube
            //    goes BLACK while the rest of the board is still plainly itself - so what the
            //    player is shown is a cube of theirs turning, on a board they can still read,
            //    rather than a marker appearing on top of one. Only THEN does the red marker
            //    settle onto it, naming the thing they will have to follow.
            GridPos at = path[0];
            SpriteRenderer blacked = ViewUtil.MakeCell(transform, "MineCube",
                board.CellToWorld(at), size * 0.92f, CoverColor, 28);
            yield return new WaitForSeconds(BlackenSeconds);

            mineMarker = ViewUtil.MakeCell(transform, "Mine", board.CellToWorld(at),
                size * 0.86f, MineColor, 30);
            yield return new WaitForSeconds(RevealSeconds);

            // 2. The covers come down over everything, the mine's among them. Its cell is
            //    already black, so the board closes AROUND it rather than over it.
            Destroy(blacked.gameObject); // the cover that lands on this cell replaces it exactly
            for (int x = model.MinX; x < model.MinX + model.Width; x++)
            {
                for (int y = model.MinY; y < model.MinY + model.Height; y++)
                {
                    var cell = new GridPos(x, y);
                    if (!model.IsInside(cell))
                    {
                        continue;
                    }
                    covers[cell] = ViewUtil.MakeCell(transform, "Cover_" + x + "_" + y,
                        board.CellToWorld(cell), size * 0.92f, CoverColor, 29);
                }
            }
            // THE RED GOES OUT HERE, and this is the whole shell game. Once the covers are down
            // the mine's is one of them and nothing else: no badge rides it, so what the player
            // follows is the MOTION of a cover, the way they would follow a cup on a table. A
            // marker riding on top made the dance a formality - you cannot lose a red square.
            // The hop below arcs the two covers past each other precisely so there is something
            // to follow without one.
            Destroy(mineMarker.gameObject);
            mineMarker = null;
            yield return new WaitForSeconds(0.2f);

            // 3. The dance. Every hop swaps the mine's cover with the one it is moving to, so the
            //    board of covers stays a board of covers and the eye has something to track.
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

        /// <summary>Swaps the two covers, arcing them past each other. The marker is gone by the
        /// time this runs (see above), so the arc is all there is to follow - which is the
        /// point.</summary>
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
