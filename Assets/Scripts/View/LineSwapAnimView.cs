// PURPOSE: Two rows (or two columns) TRADING PLACES - "Kentsel Dönüşüm"'s swap, drawn. The rules
// swap the lines in one step and the board repaints them already exchanged; this plays what happened
// in between, so the player sees the lines move rather than the board flicker into a new picture.
//
// Four beats in about two thirds of a second, one clock:
//   LIFT    both lines' cubes rise off the board - a little larger, a soft shadow opening under each;
//   TRAVEL  each line carries to the other's place on an eased path, the two BOWING APART sideways
//           so they pass one another instead of sliding through each other, with a small ripple
//           along the line (the cubes leave a hair apart, never as one rigid plank);
//   LAND    a short squash as each cube sets down and the shadow closes;
//   RELEASE the held cells are given back and repainted as they really are.
//
// The copies are drawn from the FACES the board was showing before the swap (BoardView.TryCubeLook),
// captured by the controller before the power runs; the destination cells are held blank
// (BoardView.HoldCells) until the copies land - the same bargain BossMoveView and the press make, so
// a cube never shows twice. A cell with nothing on it simply has nothing to carry. Presentation
// only: which lines, and that they swapped, is the rules' answer.

using System;
using System.Collections;
using System.Collections.Generic;
using ProjectBlock.Core;
using UnityEngine;

namespace ProjectBlock.View
{
    /// <summary>Plays a line swap on the board.</summary>
    public sealed class LineSwapAnimView : MonoBehaviour
    {
        private const float LiftSeconds = 0.12f;
        private const float TravelSeconds = 0.36f;
        private const float LandSeconds = 0.12f;
        private const float RippleStagger = 0.018f;   // per cube along the line
        private const float MaxRipple = 0.1f;         // the whole ripple is squeezed into this
        private const float LiftScale = 1.1f;
        private const float BowShare = 0.38f;          // sideways bow, as a share of a cell
        private const int Order = 30;

        private static readonly Color ShadowColour = new Color(0f, 0f, 0f, 0.35f);

        /// <summary>What was standing in the two lines before the swap, cell by cell.</summary>
        public sealed class Capture
        {
            public LineAxis Axis;
            public int LineA;
            public int LineB;
            public readonly List<GridPos> Cells = new List<GridPos>();   // both lines, all cells
            internal readonly List<Carried> Cubes = new List<Carried>();
        }

        internal struct Carried
        {
            public GridPos From;
            public GridPos To;
            public Sprite Tile;
            public Color Colour;
            public int IndexAlong;
            public int Side; // -1 for line A's cubes, +1 for line B's - which way it bows
        }

        public bool Playing { get; private set; }

        /// <summary>Takes the two lines' faces off the board BEFORE the swap is applied.</summary>
        public static Capture Take(BoardView view, LineAxis axis, int lineA, int lineB)
        {
            var capture = new Capture { Axis = axis, LineA = lineA, LineB = lineB };
            GameBoard board = view != null ? view.Board : null;
            if (board == null)
            {
                return capture;
            }
            int length = axis == LineAxis.Row ? board.Width : board.Height;
            for (int i = 0; i < length; i++)
            {
                GridPos a = axis == LineAxis.Row
                    ? new GridPos(board.MinX + i, lineA) : new GridPos(lineA, board.MinY + i);
                GridPos b = axis == LineAxis.Row
                    ? new GridPos(board.MinX + i, lineB) : new GridPos(lineB, board.MinY + i);
                capture.Cells.Add(a);
                capture.Cells.Add(b);
                AddCube(view, capture, a, b, i, -1);
                AddCube(view, capture, b, a, i, 1);
            }
            return capture;
        }

        private static void AddCube(BoardView view, Capture capture, GridPos from, GridPos to,
            int along, int side)
        {
            Sprite tile;
            Color colour;
            if (view.Board.GetCube(from).HasValue && view.TryCubeLook(from, 0f, out tile, out colour))
            {
                capture.Cubes.Add(new Carried
                {
                    From = from,
                    To = to,
                    Tile = tile,
                    Colour = colour,
                    IndexAlong = along,
                    Side = side
                });
            }
        }

        /// <summary>Plays the swap. The capture's cells must already be HELD on the view;
        /// <paramref name="done"/> runs once everything has landed (the controller releases the
        /// cells there).</summary>
        public void Play(BoardView view, Capture capture, Action done)
        {
            StartCoroutine(Run(view, capture, done));
        }

        private IEnumerator Run(BoardView view, Capture capture, Action done)
        {
            Playing = true;
            float cube = view.CubeWorldSize;
            float cellStep = Vector2.Distance(view.CellToWorld(new GridPos(0, 0)),
                view.CellToWorld(new GridPos(1, 0)));
            // Sideways is across the direction of travel: rows travel in y, so they bow in x.
            Vector2 across = capture.Axis == LineAxis.Row ? Vector2.right : Vector2.up;
            int count = capture.Cubes.Count;
            var copies = new SpriteRenderer[count];
            var shadows = new SpriteRenderer[count];
            var from = new Vector2[count];
            var to = new Vector2[count];
            float stagger = RippleStagger;
            int longest = 1;
            for (int i = 0; i < count; i++)
            {
                longest = Mathf.Max(longest, capture.Cubes[i].IndexAlong + 1);
            }
            if (stagger * (longest - 1) > MaxRipple)
            {
                stagger = MaxRipple / Mathf.Max(1, longest - 1);
            }
            for (int i = 0; i < count; i++)
            {
                Carried c = capture.Cubes[i];
                from[i] = view.CellToWorld(c.From);
                to[i] = view.CellToWorld(c.To);
                shadows[i] = ViewUtil.MakeRounded(transform, "SwapShadow", from[i],
                    new Vector2(cube, cube), Color.clear, Order);
                copies[i] = ViewUtil.MakeCell(transform, "SwapCube", from[i], cube, c.Colour,
                    Order + 1 + (c.Side > 0 ? 1 : 0));
                ViewUtil.ApplyTile(copies[i], c.Tile, cube);
                copies[i].color = c.Colour;
            }

            float total = LiftSeconds + TravelSeconds + LandSeconds + stagger * (longest - 1);
            float time = 0f;
            while (time < total)
            {
                time += Time.deltaTime;
                for (int i = 0; i < count; i++)
                {
                    Carried c = capture.Cubes[i];
                    float t = time - c.IndexAlong * stagger;
                    Vector2 at;
                    float scale;
                    float shadow;
                    if (t <= LiftSeconds)
                    {
                        float k = EaseOut(Mathf.Clamp01(t / LiftSeconds));
                        at = from[i];
                        scale = Mathf.Lerp(1f, LiftScale, k);
                        shadow = k;
                    }
                    else if (t <= LiftSeconds + TravelSeconds)
                    {
                        float k = EaseInOut((t - LiftSeconds) / TravelSeconds);
                        // The bow: out to one side and back, the two lines to opposite sides.
                        float bow = Mathf.Sin(k * Mathf.PI) * BowShare * cellStep * c.Side;
                        at = Vector2.Lerp(from[i], to[i], k) + across * bow;
                        scale = LiftScale;
                        shadow = 1f;
                    }
                    else
                    {
                        float k = Mathf.Clamp01((t - LiftSeconds - TravelSeconds) / LandSeconds);
                        at = to[i];
                        // Sets down past its size a hair and settles: a squash, not a pop.
                        scale = k < 0.5f
                            ? Mathf.Lerp(LiftScale, 0.95f, k * 2f)
                            : Mathf.Lerp(0.95f, 1f, (k - 0.5f) * 2f);
                        shadow = 1f - k;
                    }
                    // The copy rides a little above its shadow while it is up.
                    float rise = (scale - 1f) / (LiftScale - 1f);
                    rise = Mathf.Clamp01(rise) * cube * 0.08f;
                    copies[i].transform.position = at + new Vector2(0f, rise);
                    copies[i].transform.localScale = new Vector3(cube * scale, cube * scale, 1f);
                    shadows[i].transform.position = at + new Vector2(cube * 0.04f, -cube * 0.06f);
                    shadows[i].size = new Vector2(cube, cube) * Mathf.Lerp(0.9f, 1.05f, shadow);
                    Color s = ShadowColour;
                    s.a *= shadow;
                    shadows[i].color = s;
                }
                yield return null;
            }

            for (int i = 0; i < count; i++)
            {
                Destroy(copies[i].gameObject);
                Destroy(shadows[i].gameObject);
            }
            Playing = false;
            if (done != null)
            {
                done();
            }
        }

        private static float EaseOut(float t)
        {
            return 1f - (1f - t) * (1f - t);
        }

        private static float EaseInOut(float t)
        {
            t = Mathf.Clamp01(t);
            return t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) * 0.5f;
        }
    }
}
