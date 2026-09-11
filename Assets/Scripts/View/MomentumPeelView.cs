// PURPOSE: "Soğuk sökülme" (Momentum Peel) - how a cube a MOVING board carried off leaves:
// "Yürüyen merdiven" riding every row up, "Merkezkaç kuvveti" flinging every cube outward. Not a
// removal variant (those are for cubes that vanished where they stood) and not an explosion: the
// cube is torn off the board BY THE MOTION, along the step Core says it was taking
// (TurnReport.LiftMotionAt) - a direction is never worked out here.
//
//   CARRY      it is already moving when it appears: no stop, no pause before it goes.
//   TENSION    its leading edge draws ahead a few percent, its trailing edge holds, it narrows a
//              hair across - pulled, not rubber.
//   LAMINATION it comes apart into three or four laminae cut ACROSS its step (a shader band on
//              the cube's own face: straight slices for a straight step, rounded chevrons that
//              follow both leading faces for a diagonal one - a diagonal cut would take the
//              corners off as triangles, which read as shards), the leading one first, each a
//              moment after the last, a pixel or two apart - material, not a card deck.
//   STREAK     each lamina keeps the momentum, stretches along the step, thins, gives its colour
//              up to cold slate and goes: a short streak, never a trail of light.
//   SNAP       the trailing lamina holds the longest, strains, lets go all at once and follows as
//              a thin sliver.
//   RESIDUE    a thin, faint smear along the path, gone in about a tenth of a second.
//
// A cube that went OVER THE EDGE is clipped by the board's own boundary as it crosses (a streak
// may show a little beyond it, fading); one that had no ground or was blocked peels where it
// stood, along the step it tried. Nothing falls, freezes, folds, flashes or shakes; the few flecks
// fly in a narrow cone along the step. Everything varies by the cell (no Random).

using System.Collections.Generic;
using ProjectBlock.Core;
using UnityEngine;

namespace ProjectBlock.View
{
    /// <summary>A cube a moving board carried off, torn away along the step it was taking.</summary>
    public sealed class MomentumPeelView : MonoBehaviour
    {
        // =================================================================== TUNING
        /// <summary>Everything that decides how the peel READS. Sizes in CELLS, times in seconds,
        /// speeds in cells per second.</summary>
        public static class Style
        {
            // ---- carry & tension ----
            /// <summary>How fast the cube is already moving when it appears - the board's forced step.</summary>
            public static float CarrySpeed = 3.0f;

            public static float TensionDuration = 0.08f;

            /// <summary>How far the leading edge draws ahead, as a share of the cube.</summary>
            public static float TensionStretch = 0.07f;

            public static float CrossCompression = 0.025f;

            // ---- lamination ----
            public static int LayerCountMin = 3;

            public static int LayerCountMax = 4;

            /// <summary>How much the laminae differ in thickness, either way.</summary>
            public static float LayerThicknessVariation = 0.22f;

            /// <summary>When the first (leading) lamina lets go.</summary>
            public static float LayerStartDelay = 0.06f;

            public static float LayerDelayStep = 0.022f;

            /// <summary>How far apart the laminae stand sideways once they are loose (0.025: 1.6 px
            /// on a 64-pixel cell).</summary>
            public static float LayerPerpendicularOffset = 0.025f;

            // ---- speeds once loose ----
            public static float LeadingSpeed = 4.2f;

            public static float SecondarySpeed = 3.4f;

            public static float TrailingSpeed = 2.8f;

            /// <summary>How much longer than the others the trailing lamina holds on.</summary>
            public static float TrailingLag = 0.07f;

            /// <summary>How far the trailing lamina strains ahead before it lets go (0.03: 2 px).</summary>
            public static float TrailingSnapAmount = 0.03f;

            // ---- the streaks ----
            /// <summary>The longest a streak may get.</summary>
            public static float StreakLength = 0.7f;

            /// <summary>How far a lamina stretches along the step by the time it is gone.</summary>
            public static float StreakStretch = 2.2f;

            public static float StreakLifetime = 0.2f;

            /// <summary>How far past the board's edge a streak still shows, fading out.</summary>
            public static float StreakOutsideReach = 0.35f;

            // ---- colour ----
            public static float DesaturationStrength = 0.65f;

            public static float ColdInfluence = 0.55f;

            // ---- flecks ----
            public static int FleckCount = 3;

            /// <summary>Degrees either side of the step. A cone, never a burst.</summary>
            public static float FleckConeAngle = 15f;

            public static float FleckLifetime = 0.2f;

            // ---- residue ----
            public static float ResidueStrength = 0.12f;

            public static float ResidueLength = 0.6f;

            public static float ResidueDuration = 0.14f;

            // ---- the group ----
            /// <summary>Seconds, from the cell: a row the escalator carries off stays one event.</summary>
            public static float TimingJitter = 0.02f;

            /// <summary>How far detail (laminae, flecks, residue) falls from N&lt;=5 to N&gt;20.</summary>
            public static float LargeCountDetailCompensation = 0.8f;
        }

        /// <summary>The lab's debug switches. Visual only; the lab puts them all back on close.</summary>
        public static class Layers
        {
            public static bool ShowTension = true;

            /// <summary>Off: the cube goes as one piece.</summary>
            public static bool ShowLamination = true;

            public static bool ShowFlecks = true;

            public static bool ShowResidue = true;

            /// <summary>Off: a cube going over the edge is not clipped by it.</summary>
            public static bool ShowBoardClip = true;

            public static void AllOn()
            {
                ShowTension = true;
                ShowLamination = true;
                ShowFlecks = true;
                ShowResidue = true;
                ShowBoardClip = true;
            }
        }

        // =================================================================== palette

        /// <summary>Slate grey-blue: what the colour goes to.</summary>
        private static readonly Color ColdTint = new Color(0.42f, 0.48f, 0.56f);

        /// <summary>Cold silver-grey: the residue, the flecks' end.</summary>
        private static readonly Color SlateColour = new Color(0.56f, 0.61f, 0.68f);

        private static readonly Color Clear = new Color(1f, 1f, 1f, 0f);

        // =================================================================== layers

        private const int ResidueOrder = 6;

        private const int BodyOrder = 8;

        private const int LaminaOrder = 9;

        private const int FleckOrder = 10;

        /// <summary>How quickly a loose lamina's speed settles from what it had to its own.</summary>
        private const float SpeedSettle = 0.05f;

        /// <summary>How quickly what is still attached slows once the first lamina has gone - the
        /// trailing part resisting.</summary>
        private const float BodyBrake = 0.04f;

        /// <summary>A slice's edge softness, in face units: well under a pixel, for anti-aliasing.</summary>
        private const float BandSoftness = 0.02f;

        // =================================================================== state

        private struct Dice
        {
            private uint state;

            public Dice(uint seed)
            {
                state = seed == 0u ? 0x9E3779B9u : seed;
            }

            public float Next()
            {
                state ^= state << 13;
                state ^= state >> 17;
                state ^= state << 5;
                return (state >> 8) * (1f / 16777216f);
            }

            public float Range(float a, float b)
            {
                return a + (b - a) * Next();
            }
        }

        private static uint Hash(int x, int y)
        {
            unchecked
            {
                uint h = (uint)(x * 73856093) ^ (uint)(y * 19349663);
                h ^= h >> 15;
                h *= 2246822519u;
                h ^= h >> 13;
                h *= 3266489917u;
                h ^= h >> 16;
                return h;
            }
        }

        /// <summary>One slice of the cube, cut across its step.</summary>
        private sealed class Lamina
        {
            public SpriteRenderer Renderer;
            /// <summary>Where it lies across the step, in slice units: -1 trailing to 1 leading.</summary>
            public float Min;
            public float Max;
            /// <summary>The trailing side it stretches from, in face units ALONG the step.</summary>
            public float Anchor;
            public float Thickness;
            public bool Trailing;
            public float Release;
            public float Speed;
            public float Life;
            public float Opacity;
            public float Stretch;
            public float Thin;
            /// <summary>Sideways, in cells, signed.</summary>
            public float Side;
            // What it has at the moment it lets go - the body's own, so nothing jumps.
            public float StartTravel;
            public float StartSpeed;
            public float StartStretch;
            public float StartCross;
        }

        private sealed class Fleck
        {
            public SpriteRenderer Renderer;
            public Vector2 From;
            public Vector2 Velocity;
            public float Start;
            public float Life;
            public float Size;
        }

        private sealed class Peel
        {
            /// <summary>The centre of the cell it stood in when the step began.</summary>
            public Vector2 At;
            /// <summary>The step, as Core reported it, made unit length.</summary>
            public Vector2 Dir;
            /// <summary>How far the face reaches along the step, in face units: 1 straight, 1.41 diagonal.</summary>
            public float Extent;
            public LiftReason Reason;
            /// <summary>What it is clipped by: the board's rect when it went over the edge, its own
            /// cell when it was blocked - it must never be drawn into the cube that stopped it.</summary>
            public Rect Clip;
            public float Start;
            public float Tension;
            public ClusterBurstView.Look Look;
            public Color Trace;
            public SpriteRenderer Body;
            public SpriteRenderer Residue;
            public readonly List<Lamina> Laminae = new List<Lamina>();
            public readonly List<Fleck> Flecks = new List<Fleck>();
        }

        private sealed class Batch
        {
            public readonly List<Peel> Peels = new List<Peel>();
            public float Clock;
            public float End;
            public float Cell;
            public float CubeSize;
            /// <summary>The board's rect, in this view's space - what a cube going over the edge
            /// is clipped by.</summary>
            public Rect Board;
            public float Lod;
        }

        private readonly List<Batch> batches = new List<Batch>();

        private readonly Stack<SpriteRenderer> spareRenderers = new Stack<SpriteRenderer>();

        private Material plainMaterial;

        private MaterialPropertyBlock block;

        private static Material peelMaterial;

        private static bool peelLooked;

        private static readonly Dictionary<Sprite, Color> traceColours = new Dictionary<Sprite, Color>();

        /// <summary>The MomentumPeel material, or null when the shader cannot be had - the plain
        /// fallback. Shader.Find works in the editor; the Resources copy survives into a build.</summary>
        private static Material PeelMaterial()
        {
            if (!peelLooked)
            {
                peelLooked = true;
                Shader shader = Shader.Find("ProjectBlock/MomentumPeel");
                if (shader == null)
                {
                    shader = Resources.Load<Shader>("Shaders/MomentumPeel");
                }
                if (shader != null && shader.isSupported)
                {
                    peelMaterial = new Material(shader);
                    peelMaterial.hideFlags = HideFlags.HideAndDontSave;
                }
            }
            return peelMaterial;
        }

        // =================================================================== driving it

        /// <summary>
        /// Tears each cube in <paramref name="looks"/> off the board: it stood at
        /// <paramref name="cells"/>[i] (world centres), was taking <paramref name="steps"/>[i] and
        /// could not land for <paramref name="reasons"/>[i] - all three exactly as Core reported
        /// them. <paramref name="board"/> is the board's rect in the same space; a cube that
        /// EXITED is clipped by it.
        /// </summary>
        public void Play(IReadOnlyList<Vector2> cells, IReadOnlyList<Vector2> steps,
            IReadOnlyList<LiftReason> reasons, IReadOnlyList<ClusterBurstView.Look> looks,
            float cellSize, float cubeSize, Rect board)
        {
            if (cells == null || steps == null || cells.Count == 0 || cellSize <= 0f)
            {
                return;
            }
            Material peel = PeelMaterial();
            int n = cells.Count;
            int tier = n <= 5 ? 0 : n <= 12 ? 1 : n <= 20 ? 2 : 3;
            var batch = new Batch
            {
                Cell = cellSize,
                CubeSize = cubeSize > 0f ? cubeSize : cellSize,
                Board = board,
                Lod = (tier == 0 ? 0f : tier == 1 ? 0.35f : tier == 2 ? 0.65f : 1f)
                    * Mathf.Clamp01(Style.LargeCountDetailCompensation)
            };
            Sprite fallbackTile = ViewUtil.DefaultTile != null ? ViewUtil.DefaultTile
                : ViewUtil.WhiteSprite;
            for (int i = 0; i < n && i < steps.Count; i++)
            {
                Vector2 step = steps[i];
                if (step.sqrMagnitude < 1e-6f)
                {
                    continue; // no step to peel along - and never a guessed one
                }
                uint seed = Hash(Mathf.RoundToInt(cells[i].x / cellSize * 4f),
                    Mathf.RoundToInt(cells[i].y / cellSize * 4f));
                var dice = new Dice(seed);
                var p = new Peel { At = cells[i], Dir = step.normalized };
                p.Extent = Mathf.Abs(p.Dir.x) + Mathf.Abs(p.Dir.y);
                p.Reason = reasons != null && i < reasons.Count ? reasons[i] : LiftReason.ExitedBoard;
                p.Clip = p.Reason == LiftReason.Blocked
                    ? new Rect(p.At.x - cellSize * 0.5f, p.At.y - cellSize * 0.5f, cellSize, cellSize)
                    : board;
                // A row the escalator carries off is ONE event: a few tens of milliseconds
                // between its cubes, never a wave.
                p.Start = n > 1 ? dice.Range(0f, 2f) * Style.TimingJitter : 0f;
                p.Tension = Style.TensionStretch * dice.Range(0.85f, 1.15f);
                bool known = looks != null && i < looks.Count && looks[i].Tile != null;
                p.Look = known ? looks[i] : new ClusterBurstView.Look
                {
                    Tile = fallbackTile,
                    Colour = new Color(0.62f, 0.68f, 0.82f)
                };
                p.Trace = TraceColour(p.Look);
                BuildLaminae(batch, p, ref dice, peel);
                AddFlecks(batch, p, ref dice);
                p.Body = Rent(p.Look.Tile, BodyOrder, peel != null ? peel : ViewUtil.TileMaterial(p.Look.Tile));
                if (peel != null)
                {
                    p.Residue = Rent(ResidueSprite(), ResidueOrder, peel);
                }
                // On THIS frame: the board repainted a moment ago, and the cube is already moving.
                PaintPeel(batch, p, -p.Start);
                batch.Peels.Add(p);
                batch.End = Mathf.Max(batch.End, p.Start + PeelEnd(p));
            }
            if (batch.Peels.Count > 0)
            {
                batches.Add(batch);
            }
        }

        /// <summary>Cuts the face into laminae across the step - leading first - and works out,
        /// for each, when it lets go and what it has at that moment.</summary>
        private void BuildLaminae(Batch batch, Peel p, ref Dice dice, Material peel)
        {
            int min = Mathf.Clamp(Style.LayerCountMin, 1, 6);
            int max = Mathf.Clamp(Style.LayerCountMax, min, 6);
            int count;
            if (!Layers.ShowLamination || peel == null)
            {
                count = 1;
            }
            else if (batch.Lod >= 0.45f)
            {
                count = min;
            }
            else
            {
                count = min + Mathf.Min(max - min, (int)(dice.Next() * (max - min + 1)));
            }
            var weights = new float[count];
            float sum = 0f;
            for (int k = 0; k < count; k++)
            {
                weights[k] = 1f + Style.LayerThicknessVariation * dice.Range(-1f, 1f);
                sum += weights[k];
            }
            float half = batch.CubeSize * 0.5f;
            bool blocked = p.Reason == LiftReason.Blocked;
            float cursor = 1f;
            float release = Style.LayerStartDelay;
            for (int k = 0; k < count; k++)
            {
                float width = weights[k] / sum * 2f;
                var lam = new Lamina();
                // The outermost slices take everything beyond the body too (a fox's fur).
                lam.Max = k == 0 ? 10f : cursor;
                cursor -= width;
                lam.Min = k == count - 1 ? -10f : cursor;
                // A slice's trailing side, along the step: its inner corner on a diagonal.
                lam.Anchor = Mathf.Max(lam.Min, -1f) * p.Extent;
                lam.Trailing = k == count - 1 && count > 1;
                if (count == 1)
                {
                    lam.Release = Style.LayerStartDelay + Style.TrailingLag * 0.5f;
                }
                else if (lam.Trailing)
                {
                    lam.Release = release + Style.LayerDelayStep
                        + Style.TrailingLag * dice.Range(0.8f, 1.2f);
                }
                else
                {
                    lam.Release = release;
                    release += Style.LayerDelayStep * dice.Range(0.75f, 1.25f);
                }
                float speed = k == 0 ? Style.LeadingSpeed
                    : lam.Trailing ? Style.TrailingSpeed : Style.SecondarySpeed;
                lam.Speed = speed * batch.Cell * dice.Range(0.9f, 1.1f) * (blocked ? 0.55f : 1f);
                // The leading lamina is quickest and gone soonest; the trailing one lingers.
                lam.Life = Style.StreakLifetime * (k == 0 && count > 1 ? 0.8f : lam.Trailing ? 1.1f : 1f)
                    * dice.Range(0.9f, 1.1f) * (blocked ? 0.8f : 1f);
                lam.Opacity = k == 0 ? 1f : lam.Trailing ? 0.8f : 0.9f;
                lam.Thickness = Mathf.Max(width * half, 0.02f * batch.Cell);
                lam.Stretch = Mathf.Max(1f, Mathf.Min(Style.StreakStretch * dice.Range(0.9f, 1.1f),
                    Style.StreakLength * batch.Cell / lam.Thickness));
                // The last of it goes as a sliver.
                lam.Thin = lam.Trailing ? 0.6f : 0.35f;
                lam.Side = (k % 2 == 0 ? 1f : -1f) * Style.LayerPerpendicularOffset * (k == 0 ? 0.5f : 1f);
                p.Laminae.Add(lam);
            }
            float carry = Carry(batch, p);
            for (int k = 0; k < p.Laminae.Count; k++)
            {
                Lamina lam = p.Laminae[k];
                float stretch;
                float cross;
                BodyShape(batch, p, lam.Release, out stretch, out cross);
                // Exactly where the attached body had it when it let go, so nothing jumps.
                lam.StartTravel = BodyTravel(p, lam.Release, carry)
                    + (lam.Anchor + p.Extent) * half * (stretch - 1f);
                lam.StartStretch = stretch;
                lam.StartCross = cross;
                // The trailing lamina SNAPS free: it lets go faster than it will travel.
                lam.StartSpeed = lam.Trailing ? lam.Speed * 1.8f : BodySpeed(p, lam.Release, carry);
                lam.Renderer = Rent(p.Look.Tile, LaminaOrder, peel != null ? peel : ViewUtil.TileMaterial(p.Look.Tile));
            }
        }

        /// <summary>A few flecks off the leading edge as it goes, in a narrow cone along the step.</summary>
        private void AddFlecks(Batch batch, Peel p, ref Dice dice)
        {
            float secondary = Mathf.Pow(1f - batch.Lod, 1.5f);
            int count = Mathf.Clamp(Mathf.FloorToInt(Style.FleckCount * secondary + dice.Next()), 0, 5);
            if (p.Laminae.Count == 0 || PeelMaterial() == null)
            {
                return;
            }
            float half = batch.CubeSize * 0.5f;
            float from = p.Laminae[0].Release;
            float travel = BodyTravel(p, from, Carry(batch, p));
            var across = new Vector2(-p.Dir.y, p.Dir.x);
            float cone = Style.FleckConeAngle * Mathf.Deg2Rad;
            for (int f = 0; f < count; f++)
            {
                var fl = new Fleck();
                float angle = dice.Range(-1f, 1f) * cone;
                float c = Mathf.Cos(angle);
                float s = Mathf.Sin(angle);
                var dir = new Vector2(p.Dir.x * c - p.Dir.y * s, p.Dir.x * s + p.Dir.y * c);
                float side = dice.Range(-0.35f, 0.35f);
                fl.From = p.At + p.Dir * (travel + half * 0.8f) + across * (side * batch.CubeSize);
                fl.Velocity = dir * (Style.LeadingSpeed * dice.Range(0.5f, 0.8f) * batch.Cell);
                fl.Start = from + dice.Range(0f, 0.04f);
                fl.Life = Style.FleckLifetime * dice.Range(0.8f, 1.2f);
                fl.Size = dice.Range(0.022f, 0.034f) * batch.Cell;
                fl.Renderer = Rent(FleckSprite(), FleckOrder, peelMaterial);
                p.Flecks.Add(fl);
            }
        }

        private static float PeelEnd(Peel p)
        {
            float end = 0f;
            for (int k = 0; k < p.Laminae.Count; k++)
            {
                end = Mathf.Max(end, p.Laminae[k].Release + p.Laminae[k].Life);
            }
            if (p.Laminae.Count > 0)
            {
                end = Mathf.Max(end, ResidueStart(p) + Style.ResidueDuration);
            }
            for (int f = 0; f < p.Flecks.Count; f++)
            {
                end = Mathf.Max(end, p.Flecks[f].Start + p.Flecks[f].Life);
            }
            return end;
        }

        private static float ResidueStart(Peel p)
        {
            return p.Laminae[p.Laminae.Count - 1].Release + 0.04f;
        }

        /// <summary>Stops everything at once - the run ending, the board being torn down.</summary>
        public void Stop()
        {
            for (int i = 0; i < batches.Count; i++)
            {
                Release(batches[i]);
            }
            batches.Clear();
        }

        private static float Smooth(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * (3f - 2f * x);
        }

        // =================================================================== the motion

        /// <summary>The forced step's speed, world units per second. A blocked cube barely moves.</summary>
        private static float Carry(Batch batch, Peel p)
        {
            return Style.CarrySpeed * batch.Cell * (p.Reason == LiftReason.Blocked ? 0.3f : 1f);
        }

        /// <summary>How far what is still attached has gone: at the carry speed until the first
        /// lamina lets go, then braking hard - the part that is left resists.</summary>
        private static float BodyTravel(Peel p, float t, float carry)
        {
            float first = p.Laminae.Count > 0 ? p.Laminae[0].Release : 0f;
            if (t <= first)
            {
                return carry * Mathf.Max(t, 0f);
            }
            return carry * first + carry * BodyBrake * (1f - Mathf.Exp(-(t - first) / BodyBrake));
        }

        private static float BodySpeed(Peel p, float t, float carry)
        {
            float first = p.Laminae.Count > 0 ? p.Laminae[0].Release : 0f;
            return t <= first ? carry : carry * Mathf.Exp(-(t - first) / BodyBrake);
        }

        /// <summary>The attached body's shape: its leading edge drawn ahead a few percent from a
        /// trailing edge that holds, a hair narrower across; pressed a hair shorter first when it
        /// was pushed into something; and, once only the trailing lamina is left, straining.</summary>
        private static void BodyShape(Batch batch, Peel p, float t, out float stretch, out float cross)
        {
            float tension = Layers.ShowTension ? p.Tension : 0f;
            float d = Mathf.Max(Style.TensionDuration, 0.0001f);
            float a = Smooth(t / d);
            if (p.Reason == LiftReason.Blocked)
            {
                stretch = 1f - 0.03f * Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 0.1f))
                    + tension * 0.5f * Smooth((t - 0.05f) / d);
            }
            else
            {
                stretch = 1f + tension * a;
            }
            cross = 1f - (Layers.ShowTension ? Style.CrossCompression : 0f) * a;
            int count = p.Laminae.Count;
            if (count > 1 && Layers.ShowTension)
            {
                Lamina last = p.Laminae[count - 1];
                Lamina before = p.Laminae[count - 2];
                if (t > before.Release)
                {
                    float strain = Style.TrailingSnapAmount * batch.Cell / Mathf.Max(last.Thickness, 0.0001f);
                    stretch += strain * Smooth((t - before.Release)
                        / Mathf.Max(last.Release - before.Release, 0.0001f));
                }
            }
        }

        private static float LaminaTravel(Lamina lam, float u)
        {
            return lam.StartTravel + lam.Speed * u
                + (lam.StartSpeed - lam.Speed) * SpeedSettle * (1f - Mathf.Exp(-u / SpeedSettle));
        }

        // =================================================================== the clock

        private void Update()
        {
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            for (int b = batches.Count - 1; b >= 0; b--)
            {
                Batch batch = batches[b];
                batch.Clock += dt;
                for (int i = 0; i < batch.Peels.Count; i++)
                {
                    Peel p = batch.Peels[i];
                    PaintPeel(batch, p, batch.Clock - p.Start);
                }
                if (batch.Clock >= batch.End)
                {
                    Release(batch);
                    batches.RemoveAt(b);
                }
            }
        }

        /// <summary>One cube at its own age: carried, pulled, coming apart, streaking away.</summary>
        private void PaintPeel(Batch batch, Peel p, float t)
        {
            float cell = batch.Cell;
            bool clip = p.Reason == LiftReason.ExitedBoard && Layers.ShowBoardClip;
            bool walled = p.Reason == LiftReason.Blocked;
            float carry = Carry(batch, p);
            int released = 0;
            while (released < p.Laminae.Count && t >= p.Laminae[released].Release)
            {
                released++;
            }

            // ---- the body: whatever has not come away yet ----
            if (released >= p.Laminae.Count)
            {
                p.Body.color = Clear;
            }
            else
            {
                float stretch;
                float cross;
                BodyShape(batch, p, t, out stretch, out cross);
                float travel = BodyTravel(p, t, carry);
                float front = p.Laminae[released].Max;
                float cool = released > 0 ? 0.12f * Smooth((t - p.Laminae[0].Release) / 0.12f) : 0f;
                if (peelMaterial != null)
                {
                    // A hair past the slice that has just gone, under it: two soft edges meeting
                    // would let the board show through as a dark cut line.
                    Place(p.Body, p.At, batch.CubeSize, p.Look.Colour, 1f);
                    Apply(p.Body, batch, p, -10f, front + 2f * BandSoftness, 0f, travel, stretch, cross,
                        -p.Extent, 0f, cool, cool * 0.8f, clip ? 0.02f * cell : walled ? 0.03f * cell : 0f);
                }
                else
                {
                    // Plain sprites: carried along the step, and that is all they can do.
                    Place(p.Body, p.At + p.Dir * travel, batch.CubeSize, p.Look.Colour, 1f);
                }
            }

            // ---- the laminae that have let go ----
            for (int k = 0; k < p.Laminae.Count; k++)
            {
                Lamina lam = p.Laminae[k];
                float u = t - lam.Release;
                if (u < 0f || u >= lam.Life)
                {
                    lam.Renderer.color = Clear;
                    continue;
                }
                float k01 = u / lam.Life;
                float streak = 1f - (1f - k01) * (1f - k01);
                float travel = LaminaTravel(lam, u);
                float alpha = lam.Opacity * (1f - Smooth((k01 - 0.45f) / 0.55f));
                float desat = Style.DesaturationStrength * Smooth(k01 / 0.7f);
                float cold = Style.ColdInfluence * Smooth(k01 / 0.7f);
                if (peelMaterial != null)
                {
                    float stretch = Mathf.Lerp(lam.StartStretch, lam.Stretch, streak);
                    float cross = lam.StartCross * (1f - lam.Thin * streak);
                    float across = lam.Side * cell * Smooth(u / 0.04f);
                    Place(lam.Renderer, p.At, batch.CubeSize, p.Look.Colour, alpha);
                    Apply(lam.Renderer, batch, p, lam.Min, lam.Max, streak, travel, stretch, cross,
                        lam.Anchor, across, desat, cold,
                        clip ? Mathf.Lerp(0.02f, Style.StreakOutsideReach, streak) * cell
                            : walled ? 0.03f * cell : 0f);
                }
                else
                {
                    Place(lam.Renderer, p.At + p.Dir * travel, batch.CubeSize,
                        Color.Lerp(p.Look.Colour, ColdTint, cold), alpha);
                }
            }

            PaintFlecks(batch, p, t, clip, walled);
            PaintResidue(batch, p, t, clip, walled);
        }

        /// <summary>The flecks: along the step, slowing a little, gone. Material, not sparks.</summary>
        private void PaintFlecks(Batch batch, Peel p, float t, bool clip, bool walled)
        {
            for (int f = 0; f < p.Flecks.Count; f++)
            {
                Fleck fl = p.Flecks[f];
                float u = t - fl.Start;
                if (!Layers.ShowFlecks || u <= 0f || u >= fl.Life)
                {
                    fl.Renderer.color = Clear;
                    continue;
                }
                float k = u / fl.Life;
                Vector2 at = fl.From + fl.Velocity * (u * (1f - 0.35f * k));
                Color colour = Color.Lerp(Color.Lerp(p.Trace, ColdTint, 0.45f), SlateColour, k);
                float alpha = 0.55f * Smooth(k / 0.15f) * (1f - Smooth((k - 0.4f) / 0.6f));
                float angle = Mathf.Atan2(fl.Velocity.y, fl.Velocity.x) * Mathf.Rad2Deg;
                PlaceRect(fl.Renderer, at, fl.Size * 2.6f, fl.Size, colour, alpha, angle);
                Apply(fl.Renderer, batch, p, -10f, 10f, 0f, 0f, 1f, 1f, 0f, 0f, 0f, 0f,
                    clip ? Style.StreakOutsideReach * batch.Cell : walled ? 0.03f * batch.Cell : 0f);
            }
        }

        /// <summary>A thin, faint smear along the path it took, just after it has gone.</summary>
        private void PaintResidue(Batch batch, Peel p, float t, bool clip, bool walled)
        {
            if (p.Residue == null || p.Laminae.Count == 0)
            {
                return;
            }
            float k = (t - ResidueStart(p)) / Mathf.Max(Style.ResidueDuration, 0.0001f);
            if (!Layers.ShowResidue || k <= 0f || k >= 1f)
            {
                p.Residue.color = Clear;
                return;
            }
            float alpha = Style.ResidueStrength * (1f - 0.3f * batch.Lod)
                * (k < 0.2f ? k / 0.2f : 1f - Smooth((k - 0.2f) / 0.8f));
            float length = Style.ResidueLength * batch.Cell;
            Vector2 centre = p.At + p.Dir * (length * 0.5f - 0.15f * batch.Cell);
            float angle = Mathf.Atan2(p.Dir.y, p.Dir.x) * Mathf.Rad2Deg;
            PlaceRect(p.Residue, centre, length, 0.3f * batch.CubeSize, SlateColour, alpha, angle);
            Apply(p.Residue, batch, p, -10f, 10f, 0f, 0f, 1f, 1f, 0f, 0f, 0f, 0f,
                clip ? 0.04f * batch.Cell : walled ? 0.03f * batch.Cell : 0f);
        }

        /// <summary>The cube's old colour, for the flecks to carry a trace of: its tint on the plain
        /// tile it is tinted on, else the colour of the element whose face it wears.</summary>
        private static Color TraceColour(ClusterBurstView.Look look)
        {
            if (!ViewUtil.CarriesOwnPaint(look.Tile))
            {
                return look.Colour;
            }
            Color colour;
            if (traceColours.TryGetValue(look.Tile, out colour))
            {
                return colour;
            }
            colour = SlateColour;
            foreach (BlockElement element in System.Enum.GetValues(typeof(BlockElement)))
            {
                if (ViewUtil.CubeTile(element) == look.Tile)
                {
                    colour = ViewUtil.ElementColor(element);
                    break;
                }
            }
            traceColours[look.Tile] = colour;
            return colour;
        }

        // =================================================================== the shader's inputs

        private static readonly int DirId = Shader.PropertyToID("_Dir");

        private static readonly int CentreId = Shader.PropertyToID("_Centre");

        private static readonly int HalfId = Shader.PropertyToID("_Half");

        private static readonly int BandId = Shader.PropertyToID("_Band");

        private static readonly int MotionId = Shader.PropertyToID("_Motion");

        private static readonly int AcrossId = Shader.PropertyToID("_Across");

        private static readonly int ToneId = Shader.PropertyToID("_Tone");

        private static readonly int ColdTintId = Shader.PropertyToID("_ColdTint");

        private static readonly int ClipId = Shader.PropertyToID("_Clip");

        private static readonly int ClipFadeId = Shader.PropertyToID("_ClipFade");

        /// <summary>Hands one renderer its slice, motion, tone and clip. One reused block, so
        /// nothing is allocated per frame. The shader works in world space; everything here is
        /// placed in this view's.</summary>
        private void Apply(SpriteRenderer r, Batch batch, Peel p, float bandMin, float bandMax,
            float streak, float travel, float stretch, float cross, float anchor, float across,
            float desat, float cold, float clipFade)
        {
            if (peelMaterial == null || r.sharedMaterial != peelMaterial)
            {
                return;
            }
            if (block == null)
            {
                block = new MaterialPropertyBlock();
            }
            Vector3 centre = transform.TransformPoint(new Vector3(p.At.x, p.At.y, 0f));
            Vector3 lo = transform.TransformPoint(new Vector3(p.Clip.xMin, p.Clip.yMin, 0f));
            Vector3 hi = transform.TransformPoint(new Vector3(p.Clip.xMax, p.Clip.yMax, 0f));
            float scale = transform.lossyScale.x;
            block.Clear();
            block.SetVector(DirId, new Vector4(p.Dir.x, p.Dir.y, 0f, 0f));
            block.SetVector(CentreId, new Vector4(centre.x, centre.y, 0f, 0f));
            block.SetFloat(HalfId, batch.CubeSize * 0.5f * scale);
            block.SetVector(BandId, new Vector4(bandMin, bandMax, BandSoftness, streak));
            block.SetVector(MotionId, new Vector4(travel * scale, stretch, cross, anchor));
            block.SetFloat(AcrossId, across * scale);
            block.SetVector(ToneId, new Vector4(desat, cold, 0f, 0f));
            block.SetColor(ColdTintId, ColdTint);
            block.SetVector(ClipId, new Vector4(lo.x, lo.y, hi.x, hi.y));
            block.SetFloat(ClipFadeId, clipFade * scale);
            r.SetPropertyBlock(block);
        }

        // =================================================================== renderers

        private static void Place(SpriteRenderer r, Vector2 at, float size, Color colour, float alpha)
        {
            colour.a = Mathf.Clamp01(alpha);
            r.color = colour;
            r.transform.localPosition = new Vector3(at.x, at.y, 0f);
            r.transform.localRotation = Quaternion.identity;
            r.transform.localScale = new Vector3(size, size, 1f);
        }

        /// <summary>A non-square sprite sized to <paramref name="width"/> by
        /// <paramref name="height"/> and turned by <paramref name="angle"/> degrees.</summary>
        private static void PlaceRect(SpriteRenderer r, Vector2 at, float width, float height,
            Color colour, float alpha, float angle)
        {
            colour.a = Mathf.Clamp01(alpha);
            r.color = colour;
            Vector2 unit = r.sprite != null
                ? new Vector2(r.sprite.rect.width, r.sprite.rect.height) / r.sprite.pixelsPerUnit
                : Vector2.one;
            r.transform.localPosition = new Vector3(at.x, at.y, 0f);
            r.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
            r.transform.localScale = new Vector3(width / Mathf.Max(unit.x, 0.0001f),
                height / Mathf.Max(unit.y, 0.0001f), 1f);
        }

        private SpriteRenderer Rent(Sprite sprite, int order, Material material)
        {
            SpriteRenderer r;
            if (spareRenderers.Count > 0)
            {
                r = spareRenderers.Pop();
            }
            else
            {
                var go = new GameObject("Peel");
                go.transform.SetParent(transform, false);
                r = go.AddComponent<SpriteRenderer>();
                if (plainMaterial == null)
                {
                    plainMaterial = r.sharedMaterial;
                }
            }
            // A pooled renderer may have last worn a water tile's material or the peel's block.
            Material wanted = material != null ? material : plainMaterial;
            if (wanted != null && r.sharedMaterial != wanted)
            {
                r.sharedMaterial = wanted;
            }
            r.SetPropertyBlock(null);
            r.maskInteraction = SpriteMaskInteraction.None;
            r.sprite = sprite;
            r.sortingOrder = order;
            r.color = Clear;
            r.transform.localRotation = Quaternion.identity;
            r.transform.localScale = Vector3.one;
            r.enabled = true;
            return r;
        }

        private void Return(SpriteRenderer r)
        {
            if (r == null)
            {
                return;
            }
            r.enabled = false;
            r.SetPropertyBlock(null);
            spareRenderers.Push(r);
        }

        private void Release(Batch batch)
        {
            for (int i = 0; i < batch.Peels.Count; i++)
            {
                Peel p = batch.Peels[i];
                Return(p.Body);
                Return(p.Residue);
                for (int k = 0; k < p.Laminae.Count; k++)
                {
                    Return(p.Laminae[k].Renderer);
                }
                for (int f = 0; f < p.Flecks.Count; f++)
                {
                    Return(p.Flecks[f].Renderer);
                }
            }
        }

        // =================================================================== shared art

        private static Sprite residueSprite;

        private static Sprite fleckSprite;

        /// <summary>The residue: a soft bar, tapering at both ends - a ghost of the path.</summary>
        private static Sprite ResidueSprite()
        {
            if (residueSprite != null)
            {
                return residueSprite;
            }
            residueSprite = BuildBar(64, 16, 0.35f, 0.1f, 0.9f);
            return residueSprite;
        }

        /// <summary>A fleck: a tiny sliver, pointed at both ends - a chip of material.</summary>
        private static Sprite FleckSprite()
        {
            if (fleckSprite != null)
            {
                return fleckSprite;
            }
            fleckSprite = BuildBar(24, 8, 0.45f, 0.2f, 0.6f);
            return fleckSprite;
        }

        /// <summary>A horizontal bar: `ends` of its length taper, and across it the alpha falls
        /// off from a solid core of `core` (fraction of the height) over `soft`.</summary>
        private static Sprite BuildBar(int w, int h, float ends, float core, float soft)
        {
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float along = (x + 0.5f) / w;
                    float off = Mathf.Abs((y + 0.5f) / h - 0.5f) * 2f;
                    float a = (off <= core ? 1f : Mathf.Clamp01(1f - (off - core) / soft))
                        * Mathf.Clamp01(along / ends) * Mathf.Clamp01((1f - along) / ends);
                    px[y * w + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            }
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.hideFlags = HideFlags.HideAndDontSave;
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.SetPixels32(px);
            tex.Apply(false, false);
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), w);
        }
    }
}
