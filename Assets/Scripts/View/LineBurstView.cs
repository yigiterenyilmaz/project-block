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
// ONE BURST PER TIER, and WHICH tier is the streak's business, not this file's: the caller
// passes one and gets that art. A tier whose sheet is not drawn yet falls back to the highest
// one that is, and it falls back WHOLE - GameUiController.Feedback asks EffectiveTier too and
// colours the squares from the same answer, so a missing tier never pairs one tier's debris
// with another tier's flash.
//
// BURSTS ARE POOLED, because a single placement can clear a row AND a column at once. With one
// renderer the second Play overwrote the first and half of every plus-shaped clear went unseen.
// Each live burst now owns its renderer and its own clock, and a spent one is handed back out.
//
// HOW IT IS SIZED, and why one sheet covers every arena. BoardView fits every board into a
// FIXED box (maxBoardWorldSize, 6.5 units): a 7x7 and an 11x11 are the same size on screen and
// only their CELLS differ - 0.93 units against 0.59. A line is therefore ALWAYS 6.5 long, so a
// burst that spans one is the same size on every board and needs neither per-size art nor
// per-size numbers. Thickness is a plain world figure for the same reason: tying it to the cell
// would make the explosion shrink on the boards where it has more line to cover.
//
// IT IS STRETCHED, and the amount was chosen rather than accepted. The art is about 1.9:1, so
// spanning 6.5 units at its own proportions would stand ~3.5 units across - over half the
// arena, with the burst hiding the board it is going off on. Thickness is the dial that trades
// one against the other: at 2.0 the stretch reaches 1.73x and the debris reads as rectangles,
// which is the one distortion a game about square blocks cannot afford; at 3.0 it is honest but
// enormous. 2.6 costs tier 1 1.33x, which the chunks carry, and clears the line by about a cell -
// and the later sheets are drawn wider, so the same 2.6 costs tier 3 1.15x and tier 2 nothing at
// all. Whoever draws the next one: the wider you draw it, the less this has to distort it.
//
// It used to be a CHAIN of cell-sized bursts staggered out from the middle. That reads as three
// explosions rather than one - a line coming apart in pieces - and the line already has
// something saying it travels: CellFlashFx.RayTimes, underneath, in the squares themselves.
//
// THE SHEETS ARE REPACKED, each into whatever grid suits it - tier 1 into a 5x2, tier 2 into a
// 3x3, tier 3 into a 4x3 with two cells left empty - because they do not arrive tidy: tier 1 came
// laid out 4/3/3 with its trailing frames broken into loose sparks, which no grid can address.
// The grid is also what keeps a sheet UNDER the 2048 the importer caps at: tier 3's ten frames in
// a 5x2 would have been 2880 wide and lost 29% of every frame to the downscale, where the 4x3
// costs only 11% and two blank cells nothing at all. Slicing happens at runtime, as with the
// flame, so no meta ever has to describe the rectangles, and a tier's cell aspect is read off its
// own sheet - which is what lets tier 2's 2.61:1 and tier 3's 2.17:1 sit next to tier 1's 1.88:1
// and still come out the same thickness on the board.

using System.Collections.Generic;
using UnityEngine;

namespace ProjectBlock.View
{
    /// <summary>The explosion along a cleared line. Fire and forget: Play, then it runs itself
    /// out.</summary>
    public sealed class LineBurstView : MonoBehaviour
    {
        // =================================================================== TUNING
        /// <summary>Everything that decides how the burst READS, in one place.</summary>
        public static class Style
        {
            /// <summary>Frames per second, shared by every tier. The whole thing has to be OVER
            /// inside a second - a line clear is not an event the player waits through - and at
            /// 35 that is 0.29s for a ten-frame sheet and 0.26s for a nine-frame one. It was 22,
            /// doubled to 44 and then eased back 25% - 1.6x the original speed. The sheets are
            /// drawn, not simulated, so the only cost of running them faster is that the last
            /// frames read as a flicker; LineSweepView's timings carry the SAME factor, and the
            /// two must stay in step or the beam outlives the explosion it belongs to.</summary>
            public static float Fps = 35f;

            /// <summary>How far the burst reaches ACROSS its line, in world units - along the
            /// line it is as long as the line is. This is the dial that sets how much the art is
            /// stretched, so raising it straightens the burst out and drops it further over the
            /// board. Why 2.6 is in the header.</summary>
            public static float Thickness = 2.6f;
        }

        // =================================================================== the sheets

        /// <summary>How many streak tiers the game can ask for. Tiers are numbered from 1, the
        /// way the design talks about them: 1 is an ordinary clear, 3 the deepest streak.</summary>
        public const int MaxTier = 3;

        /// <summary>One tier's sheet: where it is and how it is laid out. Per tier rather than
        /// shared, because the tiers do NOT agree - tier 1 arrived with ten frames and tier 2
        /// with nine, and a nine-frame sheet read on a ten-frame grid plays an empty cell as its
        /// last frame, which is a blink at the end of every clear. Whoever repacks the next one
        /// picks whatever grid suits it and says so here.</summary>
        private struct Sheet
        {
            public string Path;
            public int Count;
            public int Columns;
            public int Rows;

            public Sheet(string path, int count, int columns, int rows)
            {
                Path = path;
                Count = count;
                Columns = columns;
                Rows = rows;
            }
        }

        /// <summary>Sheet per tier, indexed from 1 - slot 0 is unused so the numbers read the way
        /// they are spoken. A path with no asset behind it is not an error; see EffectiveTier.</summary>
        private static readonly Sheet[] Sheets =
        {
            default(Sheet),
            new Sheet("Art/Fx/line_burst_1_sheet", 10, 5, 2),
            new Sheet("Art/Fx/line_burst_2_sheet", 9, 3, 3),
            new Sheet("Art/Fx/line_burst_3_sheet", 10, 4, 3)
        };

        /// <summary>Just over CellFlashFx (10), so the burst covers the squares it is going off
        /// with, and still under the cards.</summary>
        private const int SortingOrder = 11;

        private static readonly Sprite[][] tierFrames = new Sprite[MaxTier + 1][];

        /// <summary>Each tier's own frame width:height, read off its sheet rather than written
        /// down. Needed because a sprite is one world unit WIDE at scale 1 (PPU is the cell
        /// width), so reaching a given world thickness means dividing by this - and a sheet
        /// redrawn at other proportions still lands at the thickness asked for.</summary>
        private static readonly float[] tierAspect = new float[MaxTier + 1];

        private static readonly bool[] tierLoaded = new bool[MaxTier + 1];

        /// <summary>The tier that will actually be DRAWN for a requested one: the highest tier
        /// at or below it whose sheet exists. All three are drawn now, so this currently answers
        /// with what it was asked - it stays because it is what lets a FOURTH tier be designed
        /// before it is painted, and because Feedback asks it too, so the squares can never be
        /// left flashing one tier's colour under another tier's debris.</summary>
        public static int EffectiveTier(int tier)
        {
            for (int t = Mathf.Clamp(tier, 1, MaxTier); t >= 1; t--)
            {
                if (Frames(t) != null)
                {
                    return t;
                }
            }
            return 1;
        }

        /// <summary>One tier's ten frames, sliced once and shared. Null when that sheet is not
        /// in the project - the normal state for a tier whose art is still to be drawn, so only
        /// tier 1 going missing is worth saying anything about.</summary>
        private static Sprite[] Frames(int tier)
        {
            if (tierLoaded[tier])
            {
                return tierFrames[tier];
            }
            tierLoaded[tier] = true;
            Sheet spec = Sheets[tier];
            var sheet = Resources.Load<Texture2D>(spec.Path);
            if (sheet == null)
            {
                if (tier == 1)
                {
                    Debug.LogWarning("[block_bonk] Line burst sheet missing: Resources/"
                        + spec.Path);
                }
                return null;
            }
            int cw = sheet.width / spec.Columns;
            int ch = sheet.height / spec.Rows;
            var built = new Sprite[spec.Count];
            for (int i = 0; i < spec.Count; i++)
            {
                int col = i % spec.Columns;
                int row = i / spec.Columns;
                // Texture y counts from the BOTTOM while the sheet reads top row first.
                int rectY = (spec.Rows - 1 - row) * ch;
                built[i] = Sprite.Create(sheet, new Rect(col * cw, rectY, cw, ch),
                    new Vector2(0.5f, 0.5f), cw);   // PPU = cell width: 1 unit wide at scale 1
            }
            tierAspect[tier] = (float)cw / ch;
            tierFrames[tier] = built;
            return built;
        }

        // =================================================================== internals

        private sealed class Burst
        {
            public SpriteRenderer Renderer;
            public Sprite[] Frames;

            /// <summary>Counts up through the animation; once it is past its own sheet's last
            /// frame it is spent,
            /// the renderer goes off and the entry is free for the next Play.</summary>
            public float Clock;

            public bool Running;
        }

        /// <summary>Live and spent bursts alike. A plus-shaped clear needs two at once and a
        /// board power can take several lines in a turn, so a spent one is reused rather than
        /// overwritten - which is exactly what the single renderer used to get wrong.</summary>
        private readonly List<Burst> bursts = new List<Burst>();

        /// <summary>Sets off the burst over one cleared line. `cells` are world centres and need
        /// not be contiguous - the burst spans from the first to the last, so a line with a hole
        /// in it still gets one explosion across the whole thing rather than two. `horizontal`
        /// is the line's own direction: a column turns the same art a quarter turn, which is why
        /// the scale below is identical for both. `tier` is the streak depth, 1-based.</summary>
        public void Play(IReadOnlyList<Vector2> cells, float cellSize, bool horizontal, int tier)
        {
            int drawn = EffectiveTier(tier);
            Sprite[] frames = Frames(drawn);
            if (cells == null || cells.Count == 0 || cellSize <= 0f || frames == null)
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

            Burst b = Take();
            b.Frames = frames;
            b.Renderer.transform.localPosition = horizontal
                ? new Vector3(alongMid, across, 0f)
                : new Vector3(across, alongMid, 0f);
            // Local x is ALWAYS the length of the line and local y always the thickness; the
            // rotation is what points them at the world. x takes the span outright, y divides
            // through the frame's aspect because the sprite is one unit WIDE, not tall.
            b.Renderer.transform.localRotation = horizontal
                ? Quaternion.identity
                : Quaternion.Euler(0f, 0f, 90f);
            b.Renderer.transform.localScale =
                new Vector3(span, Style.Thickness * tierAspect[drawn], 1f);
            b.Renderer.sprite = frames[0];
            b.Renderer.enabled = true;
            b.Clock = 0f;
            b.Running = true;
        }

        /// <summary>A spent burst, or a new one. Never grows past what a single turn actually
        /// needs at once.</summary>
        private Burst Take()
        {
            for (int i = 0; i < bursts.Count; i++)
            {
                if (!bursts[i].Running)
                {
                    return bursts[i];
                }
            }
            var go = new GameObject("Burst" + bursts.Count);
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = SortingOrder;
            sr.enabled = false;
            var made = new Burst { Renderer = sr };
            bursts.Add(made);
            return made;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            for (int i = 0; i < bursts.Count; i++)
            {
                Burst b = bursts[i];
                if (!b.Running)
                {
                    continue;
                }
                b.Clock += dt;
                int frame = Mathf.FloorToInt(b.Clock * Style.Fps);
                if (frame >= b.Frames.Length)
                {
                    b.Renderer.enabled = false;
                    b.Running = false;
                    continue;
                }
                b.Renderer.sprite = b.Frames[frame];
            }
        }
    }
}
