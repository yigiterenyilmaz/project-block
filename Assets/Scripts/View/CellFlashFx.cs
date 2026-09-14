// PURPOSE: The QUIET destruction. A set of cells STRIKES, cools back into the grid and pinches
// out - and which cells are in which beat travels, so it still reads as something that happened
// in a direction.
//
// This was once how EVERY cube died. The loud ones have since got languages of their own - a
// cleared line (LineSweepView + LineBurstView), a loose group exploding (ClusterBurstView), the
// clean sweep (BoardCleanseView), TNT (DynamiteBlastView), an infection (InfectionBurstView).
// What is left here is FlashBoard - the whole arena striking at once. Every boss that takes
// cubes off has a language of its own now (a removal variant, the momentum peel, the rot), and
// the quiet COLD palette this used to lend them went with them.
//
// IT IS DRAWN IN CELLS, ON PURPOSE. This board has no gradients and no glows anywhere: a cube
// is a flat hard-edged square that expresses itself by changing COLOUR (fire flickers, water
// waves, gold shimmers - see BoardView.AnimateElementCubes). So a blast is not an effect laid
// OVER the grid, it is the grid's own squares going off, on the same white sprite as
// everything else. Nothing here may reach for a soft texture: that is what would look
// imported.
//
// Every destruction keeps its own colour and passes it as a Palette, so the language is
// shared but a green infection still reads as the infection and a sweep as the sweep. Where sparks go with it (GameUiController.BurstParticles) they are handed the SAME
// schedule, so sparks and squares fire together - there is one clock, not two. A cleared LINE
// is the exception that throws none: its sheet is drawn with its own debris already.

using System.Collections.Generic;
using UnityEngine;

namespace ProjectBlock.View
{
    /// <summary>Self-animating destruction flash, drawn cell by cell. See Play.</summary>
    public sealed class CellFlashFx : MonoBehaviour
    {
        /// <summary>The beat the first cells strike on, before anything travels.</summary>
        public const float SparkSeconds = 0.045f;

        /// <summary>The beat the wave takes to reach the furthest cell of the set.</summary>
        public const float TravelSeconds = 0.085f;

        // ---- the life of ONE struck cell, from the moment the wave reaches it ----
        private const float StrikeSeconds = 0.04f;   // flat and bright, over-cell size
        private const float CoolSeconds = 0.06f;     // cooling back into the grid
        private const float CollapseSeconds = 0.07f; // pinching to a bar and blinking out
        private const float CellLife = StrikeSeconds + CoolSeconds + CollapseSeconds;

        /// <summary>Struck size, as a multiple of a cell. Slightly OVER one, so the squares
        /// close the gaps the grid normally leaves and a struck line reads as solid for an
        /// instant - that moment of continuity is what makes a row of squares a ray.</summary>
        private const float StrikeCells = 1.02f;

        /// <summary>Cooled size: exactly the board's own cell, so a blast settles back into the
        /// grid it came out of.</summary>
        private const float CooledCells = 0.92f;

        /// <summary>What the pinch leaves in the last frames - a bar no thinner than the
        /// dead-zone line or a circuit trace.</summary>
        private const float BarCells = 0.14f;

        /// <summary>Alpha steps the collapse blinks through. Quantised rather than faded,
        /// because a smooth ramp is the one thing this board never does.</summary>
        private static readonly float[] BlinkSteps = { 1f, 0.62f, 0.28f };

        /// <summary>Which way a struck square flattens on its way out. A line pinches ACROSS
        /// itself, so what is left is a bar lying along the line; a loose burst has no axis to
        /// pinch across and just shrinks.</summary>
        public enum Pinch
        {
            Uniform,
            AcrossRow,
            AcrossColumn
        }

        /// <summary>The three flat colours one struck cell passes through. Built from the
        /// colour the destruction already has, so every blast reads the same way without every
        /// blast having to look the same.</summary>
        public readonly struct Palette
        {
            public readonly Color Strike;
            public readonly Color Cool;
            public readonly Color Ember;

            public Palette(Color strike, Color cool, Color ember)
            {
                Strike = strike;
                Cool = cool;
                Ember = ember;
            }

            /// <summary>Something BLEW UP: it strikes near-white and cools down through its own
            /// colour into an ember.</summary>
            public static Palette Hot(Color tone)
            {
                return new Palette(
                    Color.Lerp(tone, Color.white, 0.8f),
                    tone,
                    Color.Lerp(tone, Color.black, 0.45f));
            }
        }

        private SpriteRenderer[] squares;
        private float[] startTimes;
        private Palette palette;
        private Pinch pinch;
        private float cellSize;
        private float age;
        private float lifetime;

        /// <summary>
        /// Strikes a set of cells. <paramref name="cells"/> are world positions and
        /// <paramref name="times"/> is when each of them goes off - one of the two schedules
        /// below, which the caller also hands to its particle burst so the two stay in step.
        /// </summary>
        public static void Play(Transform parent, IReadOnlyList<Vector2> cells,
            IReadOnlyList<float> times, float cell, Pinch pinch, Palette palette)
        {
            if (cells == null || cells.Count == 0 || cell <= 0f || times == null)
            {
                return;
            }
            var go = new GameObject("CellFlash");
            go.transform.SetParent(parent, false);

            CellFlashFx fx = go.AddComponent<CellFlashFx>();
            fx.cellSize = cell;
            fx.pinch = pinch;
            fx.palette = palette;
            fx.squares = new SpriteRenderer[cells.Count];
            fx.startTimes = new float[cells.Count];
            for (int i = 0; i < cells.Count; i++)
            {
                fx.startTimes[i] = i < times.Count ? times[i] : SparkSeconds;
                fx.lifetime = Mathf.Max(fx.lifetime, fx.startTimes[i] + CellLife);
                var square = new GameObject("Struck_" + i);
                square.transform.SetParent(go.transform, false);
                square.transform.localPosition = new Vector3(cells[i].x, cells[i].y, 0f);
                var renderer = square.AddComponent<SpriteRenderer>();
                renderer.sprite = ViewUtil.WhiteSprite;
                renderer.sortingOrder = 10; // over the board and its markers, under the cards
                renderer.enabled = false;   // nothing is struck yet
                fx.squares[i] = renderer;
            }
        }

        /// <summary>
        /// THE RAY: the schedule a cleared LINE strikes on, for cells given in order along it.
        /// The wave leaves the middle and reaches both ends at the same instant, so the two
        /// sides are always symmetric.
        /// </summary>
        public static float[] RayTimes(int count)
        {
            var times = new float[count];
            float middle = (count - 1) * 0.5f;
            float half = Mathf.Max(middle, 0.5f);
            for (int i = 0; i < count; i++)
            {
                times[i] = TimeToReach(Mathf.Abs(i - middle) / half);
            }
            return times;
        }

        /// <summary>
        /// THE RIPPLE: the schedule a loose group strikes on - outward from its own centre, by
        /// real distance, so a scatter goes off from the middle of the scatter and a whole
        /// board goes off from the middle of the board.
        /// </summary>
        public static float[] BurstTimes(IReadOnlyList<Vector2> cells)
        {
            var times = new float[cells.Count];
            var centre = Vector2.zero;
            for (int i = 0; i < cells.Count; i++)
            {
                centre += cells[i];
            }
            centre /= cells.Count;
            float furthest = 0f;
            for (int i = 0; i < cells.Count; i++)
            {
                furthest = Mathf.Max(furthest, (cells[i] - centre).magnitude);
            }
            for (int i = 0; i < cells.Count; i++)
            {
                float fraction = furthest > 0f ? (cells[i] - centre).magnitude / furthest : 0f;
                times[i] = TimeToReach(fraction);
            }
            return times;
        }

        /// <summary>When the wave reaches a point <paramref name="fraction"/> of the way from
        /// where it started to the furthest cell. Fast off the mark and settling into the far
        /// end, which is what makes it read as thrown rather than as a wipe.</summary>
        public static float TimeToReach(float fraction)
        {
            float k = 1f - Mathf.Clamp01(fraction);
            return SparkSeconds + TravelSeconds * (1f - k * k * k);
        }

        private void Update()
        {
            age += Time.deltaTime;
            if (age >= lifetime)
            {
                Destroy(gameObject);
                return;
            }
            for (int i = 0; i < squares.Length; i++)
            {
                Paint(squares[i], age - startTimes[i]);
            }
        }

        /// <summary>One struck cell at its own age. Three flat beats, with nothing between them
        /// softer than a change of colour. What travels is which cells are in which beat.
        /// </summary>
        private void Paint(SpriteRenderer square, float cellAge)
        {
            if (cellAge < 0f || cellAge >= CellLife)
            {
                square.enabled = false;
                return;
            }
            square.enabled = true;
            float along;
            float across;
            Color color;
            if (cellAge < StrikeSeconds)
            {
                // 1. THE STRIKE: over-cell and bright. The head of the wave.
                along = StrikeCells;
                across = StrikeCells;
                color = palette.Strike;
            }
            else if (cellAge < StrikeSeconds + CoolSeconds)
            {
                // 2. THE COOL: back down into the grid and into the blast's own colour. This is
                // the trail the head leaves behind it.
                float u = (cellAge - StrikeSeconds) / CoolSeconds;
                along = Mathf.Lerp(StrikeCells, CooledCells, u);
                across = along;
                color = Color.Lerp(palette.Strike, palette.Cool, u);
            }
            else
            {
                // 3. THE PINCH: flattened to a bar and blinked off in steps.
                float u = (cellAge - StrikeSeconds - CoolSeconds) / CollapseSeconds;
                along = CooledCells;
                across = Mathf.Lerp(CooledCells, BarCells, u * u);
                color = Color.Lerp(palette.Cool, palette.Ember, u);
                // Scaled, not overwritten: a palette that is already part-transparent (a cold
                // lift) has to stay that way as it blinks.
                color.a *= BlinkSteps[Mathf.Min((int)(u * BlinkSteps.Length), BlinkSteps.Length - 1)];
            }
            switch (pinch)
            {
                case Pinch.AcrossRow:
                    square.transform.localScale =
                        new Vector3(along * cellSize, across * cellSize, 1f);
                    break;
                case Pinch.AcrossColumn:
                    square.transform.localScale =
                        new Vector3(across * cellSize, along * cellSize, 1f);
                    break;
                default:
                    square.transform.localScale =
                        new Vector3(across * cellSize, across * cellSize, 1f);
                    break;
            }
            square.color = color;
        }
    }
}
