// PURPOSE: The painted burst that goes off along a cleared LINE - ONE explosion spanning it,
// row or column. Driven from GameUiController.Feedback's FlashLine, the one seam every line
// clear already goes through, so the Animation Lab's line entries play it for free and nothing
// else in the game had to learn about it.
//
// ONE SHEET FOR BOTH AXES. The art is drawn wide, and a column simply turns it a quarter turn:
// the burst is a symmetric star, so its long streaks come out vertical, which is what a column
// coming apart wants anyway. Rotating costs nothing at the call site either - the scale is the
// same numbers in both cases and the transform's rotation does the rest.
//
// CellFlashFx remains the thing that says a line died - this is the flourish on top of it, and
// the board still reads correctly with this file deleted.
//
// HOW IT IS SIZED, and why there is one sheet rather than three. BoardView fits every arena
// into a FIXED box (maxBoardWorldSize, 6.5 units): a 7x7 and an 11x11 are the same size on
// screen and only their CELLS differ - 0.93 units against 0.59. A line is therefore ALWAYS 6.5
// long, so a burst that spans one is the same size on every board and needs neither per-size
// art nor per-size numbers. Thickness is a plain world figure for the same reason: tying it to
// the cell would make the explosion shrink on the boards where it has more line to cover.
//
// IT IS STRETCHED, and the amount was chosen rather than accepted. The art is 1.878:1, so
// spanning 6.5 units at its own proportions would stand 3.46 units across - over half the
// arena, with the burst hiding the board it is going off on. Thickness is the dial that trades
// one against the other: at 2.0 the stretch reaches 1.73x and the debris reads as rectangles,
// which is the one distortion a game about square blocks cannot afford; at 3.0 it is honest
// but enormous. 2.6 costs 1.33x, which the chunks carry, and clears the line by about a cell.
//
// It used to be a CHAIN of cell-sized bursts staggered out from the middle. That reads as
// three explosions rather than one - a line coming apart in pieces - and the line already has
// something saying it travels: CellFlashFx.RayTimes, underneath, in the squares themselves.
//
// THE SHEET IS REPACKED. The frames arrived laid out 4/3/3 with the last three broken into
// loose sparks, which no grid can address; Tools has no part in this, it was repacked once into
// a clean 5x2 and that is what ships. Slicing happens at runtime, as with the flame, so no
// meta ever has to describe ten rectangles.

using System.Collections.Generic;
using UnityEngine;

namespace ProjectBlock.View
{
    /// <summary>The explosion along a cleared line. Fire and forget: Play, then it runs
    /// itself out.</summary>
    public sealed class LineBurstView : MonoBehaviour
    {
        // =================================================================== TUNING
        /// <summary>Everything that decides how the burst READS, in one place.</summary>
        public static class Style
        {
            /// <summary>Frames per second. The whole thing has to be OVER inside a second - a
            /// line clear is not an event the player waits through - and 10 frames at 22 comes
            /// to 0.45s, with room left over if the art ever grows more frames.</summary>
            public static float Fps = 22f;

            /// <summary>How far the burst reaches ACROSS its line, in world units - along the
            /// line it is as long as the line is. This is the dial that sets how much the art
            /// is stretched: the stretch works out at 3.46 / Thickness, so raising this
            /// straightens the burst out and drops it further over the board. Why 2.6 is in
            /// the header.</summary>
            public static float Thickness = 2.6f;
        }

        // =================================================================== the sheet

        private const int FrameCount = 10;
        private const int FrameColumns = 5;
        private const int FrameRows = 2;
        private const string SheetPath = "Art/Fx/line_burst_sheet";

        /// <summary>Just over CellFlashFx (10), so the burst covers the squares it is going off
        /// with, and still under the cards.</summary>
        private const int SortingOrder = 11;

        private static Sprite[] frames;
        private static bool sheetLoaded;

        /// <summary>A frame's own width:height, read off the sheet rather than written down.
        /// Needed because a sprite is one world unit WIDE at scale 1 (PPU is the cell width),
        /// so reaching a given world HEIGHT means dividing by this - and a redrawn sheet with
        /// different proportions then still lands at the height the caller asked for.</summary>
        private static float frameAspect = 1f;

        /// <summary>The ten frames, sliced once and shared. Null when the art is missing, and
        /// then Play does nothing - the squares still go off, which is the part that carries
        /// the meaning.</summary>
        private static Sprite[] Frames
        {
            get
            {
                if (sheetLoaded)
                {
                    return frames;
                }
                sheetLoaded = true;
                var sheet = Resources.Load<Texture2D>(SheetPath);
                if (sheet == null)
                {
                    Debug.LogWarning("[block_bonk] Line burst sheet missing: Resources/" + SheetPath);
                    return null;
                }
                int cw = sheet.width / FrameColumns;
                int ch = sheet.height / FrameRows;
                var built = new Sprite[FrameCount];
                for (int i = 0; i < FrameCount; i++)
                {
                    int col = i % FrameColumns;
                    int row = i / FrameColumns;
                    // Texture y counts from the BOTTOM while the sheet reads top row first.
                    int rectY = (FrameRows - 1 - row) * ch;
                    built[i] = Sprite.Create(sheet, new Rect(col * cw, rectY, cw, ch),
                        new Vector2(0.5f, 0.5f), cw);   // PPU = cell width: 1 unit wide at scale 1
                }
                frameAspect = (float)cw / ch;
                frames = built;
                return frames;
            }
        }

        // =================================================================== internals

        /// <summary>The one renderer, made on the first Play and reused. There is nothing to
        /// pool: a row clear puts up a single burst, and a second row clearing in the same turn
        /// restarts this one rather than stacking a copy on top of it.</summary>
        private SpriteRenderer burst;

        /// <summary>Counts up through the animation. At or past FrameCount/Fps it is spent and
        /// the renderer is off.</summary>
        private float clock;

        private bool running;

        /// <summary>Sets off the burst over one cleared line. `cells` are world centres and
        /// need not be contiguous - the burst spans from the first to the last, so a line with
        /// a hole in it still gets one explosion across the whole thing rather than two.
        /// `horizontal` is the line's own direction: a column turns the same art a quarter
        /// turn, which is why the scale below is identical for both.</summary>
        public void Play(IReadOnlyList<Vector2> cells, float cellSize, bool horizontal)
        {
            if (cells == null || cells.Count == 0 || cellSize <= 0f || Frames == null)
            {
                return;
            }

            // `along` runs down the line, `across` is the one coordinate every cell shares.
            float min = horizontal ? cells[0].x : cells[0].y;
            float max = min;
            float acrossSum = 0f;
            for (int i = 0; i < cells.Count; i++)
            {
                float along = horizontal ? cells[i].x : cells[i].y;
                min = Mathf.Min(min, along);
                max = Mathf.Max(max, along);
                acrossSum += horizontal ? cells[i].y : cells[i].x;
            }
            // The line's own extent, not the arena's: an eroded board is shorter than 6.5 and
            // the burst has no business reaching past the cells that actually died.
            float span = max - min + cellSize;
            float alongMid = (min + max) * 0.5f;
            float across = acrossSum / cells.Count;

            if (burst == null)
            {
                var go = new GameObject("Burst");
                go.transform.SetParent(transform, false);
                burst = go.AddComponent<SpriteRenderer>();
                burst.sortingOrder = SortingOrder;
            }
            burst.transform.localPosition = horizontal
                ? new Vector3(alongMid, across, 0f)
                : new Vector3(across, alongMid, 0f);
            // Local x is ALWAYS the length of the line and local y always the thickness; the
            // rotation is what points them at the world. x takes the span outright, y divides
            // through the frame's aspect because the sprite is one unit WIDE, not tall.
            burst.transform.localRotation = horizontal
                ? Quaternion.identity
                : Quaternion.Euler(0f, 0f, 90f);
            burst.transform.localScale = new Vector3(span, Style.Thickness * frameAspect, 1f);
            burst.sprite = frames[0];
            burst.enabled = true;
            clock = 0f;
            running = true;
        }

        private void Update()
        {
            if (!running)
            {
                return;
            }
            clock += Time.deltaTime;
            int frame = Mathf.FloorToInt(clock * Style.Fps);
            if (frame >= FrameCount)
            {
                burst.enabled = false;
                running = false;
                return;
            }
            burst.sprite = frames[frame];
        }
    }
}
