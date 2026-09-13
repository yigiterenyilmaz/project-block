// PURPOSE: The generic GROUP explosion - "Patlama: N hücre". Every destruction that is a loose
// handful of cells rather than a cleared LINE goes off through this: a power blast, the sweeper,
// a "Hedefli" payout, a late board-reshape clear. Driven from GameUiController.Feedback's
// FlashCells, the one seam all of them (and the Animation Lab) already call.
//
// WHAT EVERY CUBE GOES THROUGH - a small destruction of its own, not a particle emitter:
//   PRESSURE      the cube is still there (its own face, from BoardView.TryCubeLook). It darkens,
//                 draws in and trembles while a compact core packs up inside it.
//   FRACTURE      2-4 short hot cracks run from the energy point to the rim - one of a handful of
//                 preset patterns, turned and mirrored per cube, so no two neighbours match.
//   SHELL BREAK   the cube is gone on this frame. A faint ghost of its outline stays a moment,
//                 the inner core shows for a few frames, and a flare marks only a few cubes.
//   EJECTION      four families leave, each with its own size, speed, spin and life:
//                   major shell fragments  1-3 irregular pieces cut from the cube's OWN texture
//                                          (a corner, an edge, a panel), with a hot rim cooling off
//                   secondary chunks       smaller, faster, more of the element's colour
//                   micro debris           tiny shards, slivers and flecks, hot to char
//                   hot specks             the fastest and shortest: sparks of pure element colour
//   RESIDUE       an ember, a faint warm spot and a breath of haze - the last thing to go.
//
// THE WAVE. A cube starts when the pressure front reaches it: distance from the centroid times
// RadialDelay, capped so a big group still peaks in one window, with a small DETERMINISTIC jitter.
// A waiting cube twitches outward the moment before its turn, which is what makes the front read
// as something travelling through the matter rather than as cells taking turns.
//
// DENSITY IS LAYERS, NOT COUNT. Detail per cube falls in tiers as the group grows (N<=5 full,
// <=12, <=20, beyond), every family has a hard total, and cubes differ on purpose - some throw
// big pieces, some mostly crumbs - by a hash of their cell, so the same group always breaks the
// same way. Light and heat are divided by neighbour count so a packed group never becomes a glow.
//
// COLOUR. Matter keeps the cube's own colour (its texture, literally) with element light on its
// edges; the smaller the piece, the more of the element it carries, down to specks that are
// nothing else. Palette.From builds every tone from the one the caller passes.
//
// NO SCREEN SHAKE BY DEFAULT (the designer's call). ScreenImpulseStrength exists and is 0. The
// hit-stop holds only this effect's own clock for a frame or two, so the lab's speed knob and
// the game's coroutines are never touched. One callback, on the first break, lands the sound.
//
// NOT A LINE. A cleared line is LineSweepView + LineBurstView - a beam, escalating with the combo.

using System.Collections.Generic;
using UnityEngine;

namespace ProjectBlock.View
{
    /// <summary>The group explosion. Fire and forget: Play, then it runs itself out. Several can
    /// run at once, each on its own clock.</summary>
    public sealed class ClusterBurstView : MonoBehaviour
    {
        // =================================================================== TUNING
        /// <summary>Everything that decides how the blast READS, in one place. Sizes and
        /// distances are in CELLS, speeds in cells per second, times in seconds.</summary>
        public static class Style
        {
            // ---- the radial wave ----
            /// <summary>Seconds the front takes per cell of distance from the centroid.</summary>
            public static float RadialDelay = 0.045f;

            /// <summary>The most the front may take to reach the furthest cube: the common peak
            /// window. A bigger group has its front sped up, never its rings blurred.</summary>
            public static float RadialMaxSpread = 0.16f;

            /// <summary>Either way, from a hash of the cell - the same group always breaks the same.</summary>
            public static float RadialJitter = 0.005f;

            /// <summary>How far a waiting cube twitches outward as the front reaches it.</summary>
            public static float RippleStrength = 0.035f;

            public static float RippleDuration = 0.06f;

            public static float GroupCoreStrength = 0.8f;

            public static float GroupCoreSize = 0.40f;

            public static float GroupCoreMaxSize = 0.65f;

            // ---- pressure ----
            public static float PressureDuration = 0.10f;

            /// <summary>Multiplies the darkening, the tremor and the core building up.</summary>
            public static float PressureStrength = 1f;

            public static float ShellDarken = 0.32f;

            public static float ShellCompress = 0.05f;

            /// <summary>How far the shell shivers at the height of the pressure.</summary>
            public static float ShellTremor = 0.012f;

            public static float EdgeStressStrength = 0.45f;

            /// <summary>Only the last part of the pressure carries it.</summary>
            public static float EdgeStressDuration = 0.04f;

            /// <summary>Half the stress marks' thickness, as a fraction of the cube.</summary>
            public static float EdgeStressWidth = 0.018f;

            // ---- fracture ----
            public static float FractureOpacity = 0.85f;

            /// <summary>The cracks run in the last part of the pressure, ending on the break.</summary>
            public static float FractureDuration = 0.045f;

            /// <summary>How many of the preset crack patterns are in use (1-5).</summary>
            public static int FractureVariationCount = 5;

            public static float FractureWidth = 0.022f;

            // ---- the break ----
            public static float CoreStrength = 1f;

            /// <summary>The inner core on the break. Compact - a blur blob is not a core.</summary>
            public static float CoreSize = 0.26f;

            public static float CoreDuration = 0.07f;

            public static float GhostShellOpacity = 0.08f;

            public static float GhostShellDuration = 0.06f;

            /// <summary>The small ray flare. Hero cubes always, otherwise one cube in FlareEvery.</summary>
            public static float FlareStrength = 0.9f;

            public static float FlareSize = 0.36f;

            public static int FlareEvery = 4;

            /// <summary>How much harder the one or two cubes nearest the centroid break.</summary>
            public static float HeroBoost = 0.3f;

            public static float LocalLightStrength = 0.07f;

            public static float LocalLightRadius = 0.9f;

            // ---- A: major shell fragments ----
            public static int MajorFragmentCountMin = 1;

            public static int MajorFragmentCountMax = 3;

            public static float MajorFragmentSizeMin = 0.12f;

            public static float MajorFragmentSizeMax = 0.24f;

            public static float MajorFragmentSpeed = 3.6f;

            /// <summary>The fraction of its speed a major piece keeps after one second.</summary>
            public static float MajorFragmentDrag = 0.10f;

            /// <summary>Degrees per second, either way. Slow: they are heavy.</summary>
            public static float MajorFragmentRotation = 70f;

            public static float MajorFragmentGravity = 5f;

            public static float MajorFragmentLifetime = 0.30f;

            /// <summary>The share of major pieces that barely leave: dropped near the cell.</summary>
            public static float MajorFallNear = 0.3f;

            // ---- B: secondary chunks ----
            /// <summary>Per cube at full detail.</summary>
            public static float SecondaryCount = 3.2f;

            public static float SecondarySize = 0.10f;

            public static float SecondarySpeed = 5.5f;

            public static float SecondaryLifetime = 0.26f;

            public static float SecondaryRotation = 180f;

            public static float SecondaryGravity = 4f;

            // ---- C: micro debris ----
            public static float MicroDebrisCount = 5.5f;

            public static float MicroDebrisSize = 0.045f;

            public static float MicroDebrisSpeed = 8.5f;

            public static float MicroDebrisLifetime = 0.22f;

            public static float MicroDebrisRotation = 420f;

            public static float MicroDebrisGravity = 2.5f;

            // ---- D: hot specks ----
            public static float HotSpeckCount = 6f;

            public static float HotSpeckSpeed = 12f;

            public static float HotSpeckLifetime = 0.11f;

            public static float HotSpeckSize = 0.028f;

            /// <summary>Streak length at full speed. A speck shortens as it slows.</summary>
            public static float HotSpeckLength = 0.20f;

            // ---- motion ----
            /// <summary>How much of its launch speed debris from the OUTERMOST ring keeps. The middle
            /// throws clearly; the rim throws short, so the blast stays local.</summary>
            public static float OuterRingMomentum = 0.72f;

            /// <summary>Half-angle, in degrees, of the cone debris leaves a cube in.</summary>
            public static float DebrisCone = 50f;

            // ---- residue ----
            public static float HeatResidueStrength = 0.14f;

            public static float HeatResidueDuration = 0.28f;

            public static float HeatResidueSize = 0.60f;

            /// <summary>A breath of warm haze on the break - never a cloud.</summary>
            public static float HazeStrength = 0.06f;

            // ---- level of detail ----
            /// <summary>How far per-cube detail falls from the N&lt;=5 tier to the N&gt;20 tier:
            /// 0 keeps full detail at every size, 1 goes all the way down.</summary>
            public static float LargeNParticleCompensation = 0.9f;

            public static int MaxMajor = 50;

            public static int MaxSecondary = 70;

            public static int MaxMicro = 150;

            public static int MaxSpecks = 180;

            // ---- impact ----
            /// <summary>World units of camera impulse at N&gt;=20. ZERO: the designer wants the
            /// screen still. Anything above it brings a short push back, scaled down for small N.</summary>
            public static float ScreenImpulseStrength = 0f;

            public static float ScreenImpulseDuration = 0.12f;

            /// <summary>The most frames the break holds for, at HitStopFullCells and above.</summary>
            public static int HitStopFrames = 2;

            /// <summary>From here the break holds one frame; below it, none.</summary>
            public static int HitStopMinCells = 12;

            public static int HitStopFullCells = 24;

            // ---- palette ----
            /// <summary>The least HSV value a tone is lifted to before the palette is built.</summary>
            public static float PaletteMinValue = 0.62f;
        }

        /// <summary>The camera impulse the caller should give a group this size - zero while
        /// ScreenImpulseStrength is zero, which is the default.</summary>
        public static float ImpulseFor(int count)
        {
            if (count <= 0 || Style.ScreenImpulseStrength <= 0f)
            {
                return 0f;
            }
            return Style.ScreenImpulseStrength * Mathf.Clamp01((count - 1) / 19f);
        }

        // =================================================================== palette

        /// <summary>Every colour one blast uses, all from the tone the caller passed.</summary>
        public readonly struct Palette
        {
            /// <summary>The element's light: rims, stress, cracks, specks.</summary>
            public readonly Color Glow;

            /// <summary>The hottest colour - core, crack cores, the flare. A soft cream at most.</summary>
            public readonly Color Hot;

            /// <summary>The ember going out.</summary>
            public readonly Color Deep;

            /// <summary>A crumb gone cold - dark, still the tone.</summary>
            public readonly Color Char;

            public readonly Color Speck;

            private Palette(Color glow, Color hot, Color deep, Color charred, Color speck)
            {
                Glow = glow;
                Hot = hot;
                Deep = deep;
                Char = charred;
                Speck = speck;
            }

            public static Palette From(Color tone)
            {
                float h;
                float s;
                float v;
                Color.RGBToHSV(tone, out h, out s, out v);
                v = Mathf.Max(v, Style.PaletteMinValue);
                // A grey stays grey: pushing saturation on it would invent a hue (red, at h 0).
                float rich = s < 0.08f ? s : Mathf.Clamp01(s * 1.15f + 0.05f);
                Color main = Color.HSVToRGB(h, s, v);
                return new Palette(
                    Color.HSVToRGB(h, rich, v),
                    Color.Lerp(main, Color.white, 0.50f),
                    Color.HSVToRGB(h, rich, v * 0.72f),
                    Color.HSVToRGB(h, Mathf.Clamp01(rich * 1.05f), v * 0.40f),
                    Color.Lerp(main, Color.white, 0.65f));
            }
        }

        /// <summary>The face a cube wore: its tile and the colour it was painted.</summary>
        public struct Look
        {
            public Sprite Tile;

            /// <summary>The TINT the renderer needs - white on a tile that paints itself.</summary>
            public Color Colour;

            /// <summary>And what the block is actually MADE OF. A painted tile's tint is white,
            /// so anything deriving a palette from Colour derives it from white: gold ate pink
            /// until this existed. Alpha 0 means nobody filled it in.</summary>
            public Color Paint;
        }

        /// <summary>A tiny deterministic random source (xorshift). Seeded from the cells, so a
        /// group breaks the same way every time it breaks - and the lab can be compared frame
        /// for frame.</summary>
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

            public int Below(int n)
            {
                return Mathf.Min(n - 1, (int)(Next() * n));
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

        // =================================================================== layers

        /// <summary>Over the board and its markers, under the held card (12+). The shell sits
        /// where the cube it stands in for was; the flare is the top of the break.</summary>
        private const int ShellOrder = 5;

        private const int LightOrder = 6;

        private const int CrackOrder = 7;

        private const int CoreOrder = 8;

        private const int MajorOrder = 9;

        private const int RimOrder = 10;

        private const int SecondaryOrder = 10;

        private const int MicroOrder = 11;

        private const int FlareOrder = 12;

        private static readonly Color Clear = new Color(1f, 1f, 1f, 0f);

        // =================================================================== state

        private enum Kind
        {
            Major,
            Secondary,
            Micro,
            Speck,
            Flare,
            Haze
        }

        private sealed class Particle
        {
            public Kind Kind;
            public Burst Owner;
            public SpriteRenderer Renderer;
            /// <summary>Majors only: the hot edge the break left on the piece, cooling off.</summary>
            public SpriteRenderer Rim;
            public Vector2 At;
            public Vector2 Velocity;
            public float Size;
            public float Life;
            public float Age;
            /// <summary>Seconds before it is released - the families leave in stages.</summary>
            public float Delay;
            public float Angle;
            public float Spin;
            public float Gravity;
            public float Drag;
            public float Alpha;
            /// <summary>The matter's own tint, for the pieces that are matter.</summary>
            public Color Colour;
            /// <summary>The sprite's own width in world units at scale 1, so Size is always the
            /// piece's true size whatever it is cut from.</summary>
            public float Unit;
        }

        private sealed class Burst
        {
            public int Count;
            public float CellSize;
            public float CubeSize;
            public Palette Colours;
            public Vector2 Centre;
            /// <summary>The detail tier: 0 for N&lt;=5 up to 1 beyond 20.</summary>
            public float Largeness;
            public Dice Dice;

            public Vector2[] Pos;
            /// <summary>Away from the centroid; zero for a cube sitting on it (it throws a star).</summary>
            public Vector2[] Out;
            /// <summary>0 at the centroid, 1 at the furthest cube.</summary>
            public float[] Frac;
            public float[] Start;
            /// <summary>The share of light and heat this cube keeps after its neighbours.</summary>
            public float[] Light;
            public float[] Phase;
            public uint[] Seed;
            public bool[] Hero;
            public bool[] Broken;
            public Look[] Looks;
            public int[] Pattern;
            /// <summary>Quarter turns of the crack pattern, plus 4 when it is mirrored.</summary>
            public int[] Turn;
            public SpriteRenderer[] Shell;
            public SpriteRenderer[] Glow;
            public SpriteRenderer[] Stress;
            public SpriteRenderer[] Core;
            public SpriteRenderer[] Ghost;
            public SpriteRenderer[] Heat;
            public SpriteRenderer[][] Cracks;
            public SpriteRenderer GroupCore;
            public float GroupCoreSize;

            public float Clock;
            public float Hold;
            public bool HoldTaken;
            public int HoldFrames;
            public float FirstPeak;
            public float Impact;
            public float End;
            public bool FirstPeakFired;
            public bool ImpactFired;
            public System.Action OnFirstPeak;
            public System.Action OnImpact;

            public float Majors;
            public float Secondaries;
            public float Micros;
            public float Specks;
            public float MajorCarry;
            public float SecondaryCarry;
            public float MicroCarry;
            public float SpeckCarry;
            public float HazeCarry;
        }

        private readonly List<Burst> bursts = new List<Burst>();

        private readonly List<Particle> particles = new List<Particle>();

        private readonly Stack<Particle> spareParticles = new Stack<Particle>();

        private readonly Stack<SpriteRenderer> spareRenderers = new Stack<SpriteRenderer>();

        // =================================================================== driving it

        /// <summary>
        /// Sets a group off. <paramref name="cells"/> are the world centres of the cubes that go
        /// and <paramref name="looks"/> the face each wore (a null tile means none is known).
        /// <paramref name="cubeSize"/> is the size the board draws a cube at, so the shell takes
        /// over without a jump. <paramref name="onFirstPeak"/> fires on the frame the first cube
        /// breaks, <paramref name="onImpact"/> when half of them have; either may be null.
        /// </summary>
        public void Play(IReadOnlyList<Vector2> cells, IReadOnlyList<Look> looks, float cellSize,
            float cubeSize, Color tone, System.Action onFirstPeak, System.Action onImpact)
        {
            if (cells == null || cells.Count == 0 || cellSize <= 0f)
            {
                return;
            }
            int n = cells.Count;
            float tier = n <= 5 ? 0f : n <= 12 ? 0.35f : n <= 20 ? 0.65f : 1f;
            var b = new Burst
            {
                Count = n,
                CellSize = cellSize,
                CubeSize = cubeSize > 0f ? cubeSize : cellSize,
                Colours = Palette.From(tone),
                Largeness = tier,
                OnFirstPeak = onFirstPeak,
                OnImpact = onImpact,
                Pos = new Vector2[n],
                Out = new Vector2[n],
                Frac = new float[n],
                Start = new float[n],
                Light = new float[n],
                Phase = new float[n],
                Seed = new uint[n],
                Hero = new bool[n],
                Broken = new bool[n],
                Looks = new Look[n],
                Pattern = new int[n],
                Turn = new int[n],
                Shell = new SpriteRenderer[n],
                Glow = new SpriteRenderer[n],
                Stress = new SpriteRenderer[n],
                Core = new SpriteRenderer[n],
                Ghost = new SpriteRenderer[n],
                Heat = new SpriteRenderer[n],
                Cracks = new SpriteRenderer[n][],
                HoldFrames = n >= Style.HitStopFullCells ? Style.HitStopFrames
                    : n >= Style.HitStopMinCells ? Mathf.Min(1, Style.HitStopFrames) : 0
            };

            // Everything random about this group comes from its cells.
            uint seed = 2166136261u;
            Vector2 centre = Vector2.zero;
            for (int i = 0; i < n; i++)
            {
                int gx = Mathf.RoundToInt(cells[i].x / cellSize * 4f);
                int gy = Mathf.RoundToInt(cells[i].y / cellSize * 4f);
                b.Seed[i] = Hash(gx, gy);
                unchecked
                {
                    seed = (seed ^ b.Seed[i]) * 16777619u;
                }
                centre += cells[i];
            }
            b.Dice = new Dice(seed ^ (uint)n);
            centre /= n;
            b.Centre = centre;
            float furthest = 0f;
            for (int i = 0; i < n; i++)
            {
                furthest = Mathf.Max(furthest, (cells[i] - centre).magnitude);
            }
            float furthestCells = furthest / cellSize;
            float perCell = furthestCells > 0f
                ? Mathf.Min(Style.RadialDelay, Style.RadialMaxSpread / furthestCells) : 0f;
            float nearSq = 1.5f * cellSize * 1.5f * cellSize;
            Sprite fallbackTile = ViewUtil.DefaultTile != null ? ViewUtil.DefaultTile
                : ViewUtil.WhiteSprite;
            Color fallbackColour = Color.Lerp(tone, Color.black, 0.15f);
            fallbackColour.a = 1f;
            int patterns = Mathf.Clamp(Style.FractureVariationCount, 1, CrackPresets.Length);

            var peaks = new float[n];
            int hero0 = 0;
            int hero1 = -1;
            for (int i = 0; i < n; i++)
            {
                Vector2 off = cells[i] - centre;
                b.Pos[i] = cells[i];
                b.Out[i] = off.sqrMagnitude > 0.0625f * cellSize * cellSize ? off.normalized
                    : Vector2.zero;
                b.Frac[i] = furthest > 0f ? off.magnitude / furthest : 0f;
                float jitter = n > 1
                    ? ((b.Seed[i] & 1023u) / 1023f * 2f - 1f) * Style.RadialJitter : 0f;
                b.Start[i] = Mathf.Max(0f, off.magnitude / cellSize * perCell + jitter);
                peaks[i] = b.Start[i] + Style.PressureDuration;
                b.Phase[i] = ((b.Seed[i] >> 10) & 1023u) / 1023f * Mathf.PI * 2f;
                b.Pattern[i] = (int)((b.Seed[i] >> 3) % (uint)patterns);
                b.Turn[i] = (int)((b.Seed[i] >> 20) & 7u);

                int neighbours = 0;
                for (int j = 0; j < n; j++)
                {
                    if (j != i && (cells[j] - cells[i]).sqrMagnitude <= nearSq)
                    {
                        neighbours++;
                    }
                }
                b.Light[i] = 1f / (1f + 0.55f * neighbours);
                if (off.sqrMagnitude < (cells[hero0] - centre).sqrMagnitude)
                {
                    hero1 = hero0;
                    hero0 = i;
                }
                else if (i != hero0 && (hero1 < 0
                    || off.sqrMagnitude < (cells[hero1] - centre).sqrMagnitude))
                {
                    hero1 = i;
                }

                bool known = looks != null && i < looks.Count && looks[i].Tile != null;
                b.Looks[i] = known ? looks[i]
                    : new Look { Tile = fallbackTile, Colour = fallbackColour };

                // The tile's OWN material: water keeps swirling and fire keeps rolling until the
                // cube breaks - on the plain material they would freeze the moment it is hit.
                b.Shell[i] = Rent(b.Looks[i].Tile, ShellOrder, ViewUtil.TileMaterial(b.Looks[i].Tile));
                b.Glow[i] = Rent(DotSprite(), LightOrder);
                b.Heat[i] = Rent(DotSprite(), LightOrder);
                b.Stress[i] = Rent(StressSprite(), CrackOrder);
                b.Ghost[i] = Rent(RingSprite(), CrackOrder);
                b.Core[i] = Rent(DotSprite(), CoreOrder);
                // A big group gets one crack fewer per cube: the pattern still reads, the frame
                // is not a web.
                int cracks = CrackPresets[b.Pattern[i]].Length - (tier >= 0.65f ? 1 : 0);
                b.Cracks[i] = new SpriteRenderer[Mathf.Max(2, cracks) * 2];
                for (int c = 0; c < b.Cracks[i].Length; c++)
                {
                    b.Cracks[i][c] = Rent(LineSprite(), CrackOrder);
                }
                // On THIS frame, not the next: the board repainted the cell empty a moment ago
                // and the cube must not blink out before it breaks.
                Place(b.Shell[i], cells[i], b.CubeSize, b.Looks[i].Colour, 1f);
            }
            b.Hero[hero0] = true;
            if (n > 5 && hero1 >= 0)
            {
                b.Hero[hero1] = true;
            }

            var sorted = (float[])peaks.Clone();
            System.Array.Sort(sorted);
            b.FirstPeak = sorted[0];
            b.Impact = sorted[n / 2];
            b.End = sorted[n - 1] + Style.CoreDuration + Style.HeatResidueDuration;

            // Detail per cube, by tier. Rounded by a carry rather than per cube, so 1.6 pieces a
            // cube across ten cubes is sixteen pieces - never ten, never twenty.
            float lod = tier * Mathf.Clamp01(Style.LargeNParticleCompensation);
            float fullMajors = Mathf.Lerp(Style.MajorFragmentCountMin, Style.MajorFragmentCountMax, 0.6f);
            b.Majors = Mathf.Min(Mathf.Lerp(fullMajors, Style.MajorFragmentCountMin * 0.7f, lod),
                (float)Style.MaxMajor / n);
            b.Secondaries = Mathf.Min(Mathf.Lerp(Style.SecondaryCount, Style.SecondaryCount * 0.35f, lod),
                (float)Style.MaxSecondary / n);
            b.Micros = Mathf.Min(Mathf.Lerp(Style.MicroDebrisCount, Style.MicroDebrisCount * 0.45f, lod),
                (float)Style.MaxMicro / n);
            // Specks fall least: at large N the shared hot dust is the group layer.
            b.Specks = Mathf.Min(Mathf.Lerp(Style.HotSpeckCount, Style.HotSpeckCount * 0.55f, lod),
                (float)Style.MaxSpecks / n);
            b.MajorCarry = 0.5f;
            b.SecondaryCarry = 0.5f;
            b.MicroCarry = 0.5f;
            b.SpeckCarry = 0.5f;
            b.HazeCarry = 0.5f;

            if (n >= 3)
            {
                b.GroupCore = Rent(DotSprite(), FlareOrder);
                b.GroupCoreSize = Mathf.Min(Style.GroupCoreMaxSize,
                    Style.GroupCoreSize * (1f + 0.12f * Mathf.Sqrt(n)));
            }
            bursts.Add(b);
        }

        /// <summary>Stops everything at once - the run ending, the board being torn down.</summary>
        public void Stop()
        {
            for (int i = 0; i < bursts.Count; i++)
            {
                Release(bursts[i]);
            }
            bursts.Clear();
            for (int i = 0; i < particles.Count; i++)
            {
                Return(particles[i].Renderer);
                Return(particles[i].Rim);
                spareParticles.Push(particles[i]);
            }
            particles.Clear();
        }

        // =================================================================== the clock

        private void Update()
        {
            // A hitch must not throw the debris across the board in one frame.
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            for (int i = bursts.Count - 1; i >= 0; i--)
            {
                Burst b = bursts[i];
                if (b.Hold > 0f)
                {
                    b.Hold -= dt;
                }
                else
                {
                    b.Clock += dt;
                    if (!b.HoldTaken && b.Clock >= b.Impact)
                    {
                        b.HoldTaken = true;
                        if (b.HoldFrames > 0)
                        {
                            // Frozen ON the impact frame, not a few ms past it.
                            b.Clock = b.Impact;
                            b.Hold = b.HoldFrames / 60f;
                        }
                    }
                }
                if (!b.FirstPeakFired && b.Clock >= b.FirstPeak)
                {
                    b.FirstPeakFired = true;
                    if (b.OnFirstPeak != null)
                    {
                        b.OnFirstPeak();
                    }
                }
                if (!b.ImpactFired && b.Clock >= b.Impact)
                {
                    b.ImpactFired = true;
                    if (b.OnImpact != null)
                    {
                        b.OnImpact();
                    }
                }
                PaintGroupCore(b);
                PaintCells(b);
                if (b.Clock >= b.End && !OwnsParticles(b))
                {
                    Release(b);
                    bursts.RemoveAt(i);
                }
            }
            UpdateParticles(dt);
        }

        /// <summary>The pressure gathering at the centroid until the first cube breaks, then gone
        /// in a few frames - it has handed its energy on.</summary>
        private static void PaintGroupCore(Burst b)
        {
            if (b.GroupCore == null)
            {
                return;
            }
            float lead = Mathf.Max(b.FirstPeak, 0.0001f);
            float k = Mathf.Clamp01(b.Clock / lead);
            float alpha = b.Clock < lead
                ? Style.GroupCoreStrength * Mathf.Pow(k, 1.5f)
                : Style.GroupCoreStrength * Mathf.Clamp01(1f - (b.Clock - lead) / 0.05f);
            Place(b.GroupCore, b.Centre, b.CellSize * b.GroupCoreSize * Mathf.Lerp(0.6f, 1f, k),
                Color.Lerp(b.Colours.Glow, b.Colours.Hot, k), alpha);
        }

        /// <summary>Every cube at its own age: waiting, under pressure, broken.</summary>
        private void PaintCells(Burst b)
        {
            float pressure = Mathf.Max(Style.PressureDuration, 0.0001f);
            for (int i = 0; i < b.Count; i++)
            {
                float age = b.Clock - b.Start[i];
                if (age < 0f)
                {
                    // WAITING. The moment before the front arrives, the cube twitches outward -
                    // that twitch passing from ring to ring is the wave moving through the matter.
                    Vector2 twitch = Vector2.zero;
                    float lead = Mathf.Max(Style.RippleDuration, 0.0001f);
                    if (age > -lead)
                    {
                        float u = (age + lead) / lead;
                        twitch = b.Out[i] * (Mathf.Sin(u * Mathf.PI) * Style.RippleStrength
                            * b.CellSize * (1f + 0.5f * b.Largeness));
                    }
                    Place(b.Shell[i], b.Pos[i] + twitch, b.CubeSize, b.Looks[i].Colour, 1f);
                    HideCell(b, i);
                    continue;
                }
                if (age < pressure)
                {
                    PaintPressure(b, i, age, pressure);
                    continue;
                }
                if (!b.Broken[i])
                {
                    b.Broken[i] = true;
                    Break(b, i);
                }
                PaintBroken(b, i, age - pressure);
            }
        }

        private static void HideCell(Burst b, int i)
        {
            b.Glow[i].color = Clear;
            b.Heat[i].color = Clear;
            b.Stress[i].color = Clear;
            b.Core[i].color = Clear;
            b.Ghost[i].color = Clear;
            SpriteRenderer[] lines = b.Cracks[i];
            for (int c = 0; c < lines.Length; c++)
            {
                lines[c].color = Clear;
            }
        }

        /// <summary>PRESSURE and FRACTURE: the shell darkens, draws in and shivers around a
        /// compact core; the edges take the stress; the cracks run in the last few frames.</summary>
        private static void PaintPressure(Burst b, int i, float age, float pressure)
        {
            float k = age / pressure;
            float cell = b.CellSize;
            Palette p = b.Colours;
            float s = Style.PressureStrength;
            float squeeze = 1f - Style.ShellCompress * s * k * k;
            var tremor = new Vector2(Mathf.Sin(age * 295f + b.Phase[i]),
                Mathf.Cos(age * 333f + b.Phase[i] * 1.3f)) * (Style.ShellTremor * s * cell * k * k);
            Vector2 at = b.Pos[i] + tremor;
            Color shell = b.Looks[i].Colour * Mathf.Lerp(1f, 1f - Style.ShellDarken * s, k);
            Place(b.Shell[i], at, b.CubeSize * squeeze, shell, 1f);
            float hero = b.Hero[i] ? 1f + Style.HeroBoost : 1f;
            Place(b.Core[i], at, cell * Style.CoreSize * Mathf.Lerp(0.55f, 0.8f, k),
                Color.Lerp(p.Glow, p.Hot, k), Style.CoreStrength * s * hero * 0.85f * Mathf.Pow(k, 1.8f));
            float from = 1f - Mathf.Clamp01(Style.EdgeStressDuration / pressure);
            float stress = 0f;
            if (k > from)
            {
                float e = (k - from) / Mathf.Max(1f - from, 0.0001f);
                stress = Style.EdgeStressStrength * Mathf.Pow(e, 0.8f)
                    * (Mathf.Sin(age * 140f + b.Phase[i]) > 0f ? 1f : 0.55f);
            }
            Place(b.Stress[i], at, b.CubeSize * squeeze, Color.Lerp(p.Glow, p.Hot, 0.25f), stress);
            PaintCracks(b, i, age, pressure, at, b.CubeSize * squeeze);
            Place(b.Glow[i], b.Pos[i], cell * 2f * Style.LocalLightRadius, p.Glow,
                Style.LocalLightStrength * b.Light[i] * 0.3f * k);
            b.Ghost[i].color = Clear;
            b.Heat[i].color = Clear;
        }

        /// <summary>The cracks: each runs from the hot point to the rim in two legs, one after
        /// the other, and holds until the shell breaks along them.</summary>
        private static void PaintCracks(Burst b, int i, float age, float pressure, Vector2 at,
            float size)
        {
            SpriteRenderer[] lines = b.Cracks[i];
            float from = pressure - Mathf.Min(Style.FractureDuration, pressure);
            if (age < from)
            {
                for (int c = 0; c < lines.Length; c++)
                {
                    lines[c].color = Clear;
                }
                return;
            }
            float k = Mathf.Clamp01((age - from) / Mathf.Max(pressure - from, 0.0001f));
            float reveal = Mathf.Clamp01(k / 0.6f);
            float alpha = Style.FractureOpacity * Mathf.Lerp(0.6f, 1f, k);
            Color colour = Color.Lerp(b.Colours.Glow, b.Colours.Hot, 0.5f);
            float[][] preset = CrackPresets[b.Pattern[i]];
            float width = Style.FractureWidth * b.CellSize * 8f;
            for (int c = 0; c < lines.Length / 2; c++)
            {
                float[] crack = preset[c];
                Vector2 h = at + Orient(crack[0], crack[1], b.Turn[i]) * size;
                Vector2 m = at + Orient(crack[2], crack[3], b.Turn[i]) * size;
                Vector2 e = at + Orient(crack[4], crack[5], b.Turn[i]) * size;
                Segment(lines[c * 2], h, m, Mathf.Clamp01(reveal * 2f), colour, alpha, width);
                Segment(lines[c * 2 + 1], m, e, Mathf.Clamp01(reveal * 2f - 1f), colour, alpha, width);
            }
        }

        private static void Segment(SpriteRenderer r, Vector2 from, Vector2 to, float t,
            Color colour, float alpha, float width)
        {
            if (t <= 0f)
            {
                r.color = Clear;
                return;
            }
            Vector2 end = from + (to - from) * t;
            Vector2 mid = (from + end) * 0.5f;
            Vector2 d = to - from;
            r.transform.localPosition = new Vector3(mid.x, mid.y, 0f);
            r.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            r.transform.localScale = new Vector3((end - from).magnitude, width, 1f);
            colour.a = Mathf.Clamp01(alpha);
            r.color = colour;
        }

        /// <summary>A crack point in cube space, turned a quarter at a time and mirrored.</summary>
        private static Vector2 Orient(float x, float y, int turn)
        {
            if ((turn & 4) != 0)
            {
                x = -x;
            }
            for (int q = 0; q < (turn & 3); q++)
            {
                float t = x;
                x = -y;
                y = t;
            }
            return new Vector2(x, y);
        }

        /// <summary>SHELL BREAK and RESIDUE on the cube's own slot: a ghost of its outline for a
        /// moment, the inner core for a few frames then an ember, a warm spot, the light.</summary>
        private static void PaintBroken(Burst b, int i, float since)
        {
            float cell = b.CellSize;
            Palette p = b.Colours;
            Vector2 at = b.Pos[i];
            float hero = b.Hero[i] ? 1f + Style.HeroBoost : 1f;
            b.Shell[i].color = Clear;
            b.Stress[i].color = Clear;
            SpriteRenderer[] lines = b.Cracks[i];
            for (int c = 0; c < lines.Length; c++)
            {
                lines[c].color = Clear;
            }

            float g = since / Mathf.Max(Style.GhostShellDuration, 0.0001f);
            if (g < 1f)
            {
                // The element's own light, not the cream of the core: a ghost, not a frame.
                Place(b.Ghost[i], at, b.CubeSize * (1f + 0.06f * g), Color.Lerp(p.Glow, p.Deep, g),
                    Style.GhostShellOpacity * (1f - g) * (1f - g) * (1f - g));
            }
            else
            {
                b.Ghost[i].color = Clear;
            }

            float coreTime = Mathf.Max(Style.CoreDuration, 0.0001f);
            float residue = Mathf.Max(Style.HeatResidueDuration, 0.0001f);
            // The ember is shared out by density like the heat: forty embers at full strength are
            // a grid of lanterns, not the aftermath of one explosion.
            float ember = 0.32f * Mathf.Sqrt(b.Light[i]);
            if (since < coreTime)
            {
                // The inner core, seen for a few frames through the broken shell.
                float k = since / coreTime;
                float peak = Style.CoreStrength * hero;
                float a = k < 0.25f ? peak : Mathf.Lerp(peak, ember, (k - 0.25f) / 0.75f);
                Place(b.Core[i], at, cell * Style.CoreSize * (1f + 0.25f * k),
                    Color.Lerp(p.Hot, p.Glow, k * 0.6f), a);
            }
            else
            {
                // ...cooling into an ember that goes quickly.
                float k = Mathf.Clamp01((since - coreTime) / residue);
                Place(b.Core[i], at,
                    cell * Mathf.Lerp(Style.CoreSize * 1.25f, 0.14f, 1f - (1f - k) * (1f - k)),
                    Color.Lerp(Color.Lerp(p.Hot, p.Glow, 0.6f), p.Deep, k),
                    ember * Mathf.Pow(1f - k, 2.2f));
            }

            float hk = Mathf.Clamp01(since / (coreTime + residue));
            float heat = hk < 0.15f ? hk / 0.15f : Mathf.Pow(1f - (hk - 0.15f) / 0.85f, 1.3f);
            Place(b.Heat[i], at, cell * Style.HeatResidueSize, p.Deep,
                Style.HeatResidueStrength * Mathf.Sqrt(b.Light[i]) * heat);

            float lk = Mathf.Clamp01(since / (coreTime * 2f));
            Place(b.Glow[i], at, cell * 2f * Style.LocalLightRadius, p.Glow,
                Style.LocalLightStrength * b.Light[i] * hero * (1f - lk) * (1f - lk));
        }

        // =================================================================== the break

        /// <summary>Where major pieces come from: the shell's corners and edge middles, in cube
        /// units. Secondary chunks come from the inner four.</summary>
        private static readonly Vector2[] ShellSlots =
        {
            new Vector2(-0.32f, -0.32f), new Vector2(0.32f, -0.32f),
            new Vector2(0.32f, 0.32f), new Vector2(-0.32f, 0.32f),
            new Vector2(0f, -0.36f), new Vector2(0.36f, 0f),
            new Vector2(0f, 0.36f), new Vector2(-0.36f, 0f)
        };

        private static readonly Vector2[] InnerSlots =
        {
            new Vector2(-0.16f, -0.16f), new Vector2(0.16f, -0.16f),
            new Vector2(0.16f, 0.16f), new Vector2(-0.16f, 0.16f)
        };

        private static int Portion(ref float carry, float perCube)
        {
            carry += perCube;
            int count = Mathf.FloorToInt(carry);
            carry -= count;
            return count;
        }

        /// <summary>Which way a piece leaves: inside a cone around "away from the centroid",
        /// weighted to its middle - or, for a cube ON the centroid, an even star.</summary>
        private static Vector2 Launch(Burst b, int i, float cone, int index, int count)
        {
            float angle;
            if (b.Out[i] == Vector2.zero)
            {
                angle = (index + b.Dice.Range(0f, 0.35f)) / Mathf.Max(1, count) * Mathf.PI * 2f
                    + b.Phase[i];
            }
            else
            {
                angle = Mathf.Atan2(b.Out[i].y, b.Out[i].x)
                    + (b.Dice.Next() - b.Dice.Next()) * cone * Mathf.Deg2Rad;
            }
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        }

        /// <summary>The shell slot facing furthest away from the centroid - the side the
        /// pressure breaks out of first.</summary>
        private static int BestSlot(Burst b, int i)
        {
            if (b.Out[i] == Vector2.zero)
            {
                return b.Dice.Below(ShellSlots.Length);
            }
            int best = 0;
            float score = float.MinValue;
            for (int s = 0; s < ShellSlots.Length; s++)
            {
                float v = Vector2.Dot(ShellSlots[s].normalized, b.Out[i]) + b.Dice.Range(0f, 0.3f);
                if (v > score)
                {
                    score = v;
                    best = s;
                }
            }
            return best;
        }

        /// <summary>EJECTION: the four families, staged a few milliseconds apart, plus the flare
        /// for a few cubes and a breath of haze.</summary>
        private void Break(Burst b, int i)
        {
            float cell = b.CellSize;
            Palette p = b.Colours;
            Vector2 at = b.Pos[i];
            Look look = b.Looks[i];
            float momentum = Mathf.Lerp(1f, Style.OuterRingMomentum, b.Frac[i]);
            float hero = b.Hero[i] ? 1f + Style.HeroBoost : 1f;
            // Each cube has a character, from its cell: some throw big pieces, some mostly crumbs.
            int profile = (int)(b.Seed[i] % 3u);
            float majorMul = profile == 0 ? 1.4f : profile == 2 ? 0.6f : 1f;
            float microMul = profile == 0 ? 0.7f : profile == 2 ? 1.4f : 1f;
            Color matter = look.Colour;
            matter.a = 1f;

            // A: major shell fragments, cut from the cube's own face where the shell broke.
            int majors = Portion(ref b.MajorCarry, b.Majors * majorMul);
            if (b.Hero[i])
            {
                majors = Mathf.Max(majors, 1);
            }
            int first = BestSlot(b, i);
            for (int m = 0; m < majors; m++)
            {
                int slot = (first + m * 3) % ShellSlots.Length;
                Vector2 from = ShellSlots[slot];
                int shape = b.Dice.Below(MajorShapes.Length);
                float size = Mathf.Lerp(Style.MajorFragmentSizeMin, Style.MajorFragmentSizeMax,
                    Mathf.Pow(b.Dice.Next(), 1.3f)) * Mathf.Lerp(1f, 0.8f, b.Largeness);
                Vector2 dir = from.normalized * 0.8f + b.Out[i] * 0.9f
                    + new Vector2(b.Dice.Range(-0.25f, 0.25f), b.Dice.Range(-0.25f, 0.25f));
                dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : from.normalized;
                bool near = b.Dice.Next() < Style.MajorFallNear;
                // Heavier is slower; a piece that falls near barely leaves.
                float speed = Style.MajorFragmentSpeed * cell * momentum * hero
                    * Mathf.Sqrt(Style.MajorFragmentSizeMin / size) * b.Dice.Range(0.8f, 1.1f)
                    * (near ? 0.35f : 1f);
                Particle q = Emit(Kind.Major, b, at + from * b.CubeSize, dir * speed,
                    FragmentSprite(look.Tile, slot, shape, false), MajorOrder);
                q.Rim = Rent(RimSprite(shape, false), RimOrder);
                q.Size = cell * size;
                q.Life = Style.MajorFragmentLifetime * b.Dice.Range(0.85f, 1.15f) * (near ? 1.2f : 1f);
                q.Spin = b.Dice.Range(-1f, 1f) * Style.MajorFragmentRotation;
                q.Angle = b.Dice.Range(-25f, 25f);
                q.Drag = Style.MajorFragmentDrag;
                q.Gravity = Style.MajorFragmentGravity * (near ? 1.6f : 1f);
                q.Colour = matter;
            }

            // B: secondary chunks - from inside, smaller, faster, more of the element.
            int secondaries = Portion(ref b.SecondaryCarry, b.Secondaries * (b.Hero[i] ? 1.3f : 1f));
            Color chunk = Color.Lerp(matter, p.Glow, 0.35f);
            for (int s = 0; s < secondaries; s++)
            {
                int slot = b.Dice.Below(InnerSlots.Length);
                int shape = b.Dice.Below(SecondaryShapes.Length);
                Vector2 dir = Launch(b, i, Style.DebrisCone, s, secondaries);
                Particle q = Emit(Kind.Secondary, b, at + InnerSlots[slot] * b.CubeSize,
                    dir * Style.SecondarySpeed * cell * momentum * b.Dice.Range(0.75f, 1.1f),
                    FragmentSprite(look.Tile, ShellSlots.Length + slot, shape, true), SecondaryOrder);
                q.Size = cell * Style.SecondarySize * b.Dice.Range(0.75f, 1.3f);
                q.Life = Style.SecondaryLifetime * b.Dice.Range(0.8f, 1.15f);
                q.Delay = 0.008f;
                q.Spin = b.Dice.Range(-1f, 1f) * Style.SecondaryRotation;
                q.Angle = b.Dice.Range(0f, 360f);
                q.Drag = 0.06f;
                q.Gravity = Style.SecondaryGravity;
                q.Colour = chunk;
            }

            // C: micro debris - tiny shards, slivers and flecks, hot going to char.
            int micros = Portion(ref b.MicroCarry, b.Micros * microMul);
            for (int s = 0; s < micros; s++)
            {
                Vector2 dir = Launch(b, i, Style.DebrisCone * 1.3f, s, micros);
                var from = new Vector2(b.Dice.Range(-0.3f, 0.3f), b.Dice.Range(-0.3f, 0.3f));
                Particle q = Emit(Kind.Micro, b, at + from * b.CubeSize,
                    dir * Style.MicroDebrisSpeed * cell * momentum * b.Dice.Range(0.6f, 1.1f),
                    MicroSprite(b.Dice.Below(MicroShapes.Length)), MicroOrder);
                q.Size = cell * Style.MicroDebrisSize * b.Dice.Range(0.6f, 1.5f);
                q.Life = Style.MicroDebrisLifetime * b.Dice.Range(0.75f, 1.2f);
                q.Delay = 0.018f * b.Dice.Range(0.6f, 1.4f);
                q.Spin = b.Dice.Range(-1f, 1f) * Style.MicroDebrisRotation;
                q.Angle = b.Dice.Range(0f, 360f);
                q.Drag = 0.04f;
                q.Gravity = Style.MicroDebrisGravity;
            }

            // D: hot specks - the fastest and the shortest-lived, nothing but element light.
            int specks = Portion(ref b.SpeckCarry, b.Specks * (profile == 2 ? 1.2f : 1f));
            for (int s = 0; s < specks; s++)
            {
                Vector2 dir = Launch(b, i, Style.DebrisCone * 0.8f, s, specks);
                Particle q = Emit(Kind.Speck, b, at + dir * cell * 0.1f,
                    dir * Style.HotSpeckSpeed * cell * momentum * b.Dice.Range(0.7f, 1.1f),
                    DotSprite(), MicroOrder);
                q.Size = cell * Style.HotSpeckSize * b.Dice.Range(0.7f, 1.3f);
                q.Life = Style.HotSpeckLifetime * b.Dice.Range(0.7f, 1.3f);
                q.Delay = 0.004f;
                q.Drag = 0.02f;
            }

            // The flare: hero cubes, and one cube in FlareEvery - an accent, never a field of stars.
            if (b.Hero[i] || (Style.FlareEvery > 0 && b.Seed[i] % (uint)Style.FlareEvery == 0u))
            {
                Particle q = Emit(Kind.Flare, b, at, Vector2.zero, FlareSprite(), FlareOrder);
                q.Size = cell * Style.FlareSize * (b.Hero[i] ? 1.15f : 0.85f);
                q.Life = 0.07f;
                q.Angle = b.Dice.Range(0f, 45f);
                q.Alpha = Style.FlareStrength;
            }

            // A breath of warm haze - compact, and shared out by density like the heat.
            int haze = Portion(ref b.HazeCarry, Mathf.Lerp(1f, 0.35f, b.Largeness));
            for (int s = 0; s < haze; s++)
            {
                Particle q = Emit(Kind.Haze, b, at, b.Out[i] * 0.4f * cell, DotSprite(), LightOrder);
                q.Size = cell * 0.7f;
                q.Life = 0.22f * b.Dice.Range(0.85f, 1.15f);
                q.Drag = 0.1f;
                q.Alpha = Style.HazeStrength * Mathf.Sqrt(b.Light[i]);
            }
        }

        private Particle Emit(Kind kind, Burst owner, Vector2 at, Vector2 velocity, Sprite sprite,
            int order)
        {
            Particle q = spareParticles.Count > 0 ? spareParticles.Pop() : new Particle();
            q.Kind = kind;
            q.Owner = owner;
            q.At = at;
            q.Velocity = velocity;
            q.Size = 0f;
            q.Life = 0.1f;
            q.Age = 0f;
            q.Delay = 0f;
            q.Angle = 0f;
            q.Spin = 0f;
            q.Gravity = 0f;
            q.Drag = 1f;
            q.Alpha = 1f;
            q.Colour = Color.white;
            q.Rim = null;
            q.Renderer = Rent(sprite, order);
            // From the RECT, not the bounds: a fragment's overridden geometry shrinks its bounds to
            // the polygon, and the rim - drawn on a full unit quad - has to line up with it.
            q.Unit = sprite != null ? Mathf.Max(sprite.rect.width / sprite.pixelsPerUnit, 0.0001f) : 1f;
            particles.Add(q);
            return q;
        }

        private void UpdateParticles(float dt)
        {
            for (int i = particles.Count - 1; i >= 0; i--)
            {
                Particle q = particles[i];
                // Held with its blast: painted where it stands, not moved and not aged.
                float step = q.Owner.Hold > 0f ? 0f : dt;
                if (q.Delay > 0f)
                {
                    q.Delay -= step;
                    q.Renderer.color = Clear;
                    if (q.Rim != null)
                    {
                        q.Rim.color = Clear;
                    }
                    continue;
                }
                q.Age += step;
                if (q.Age >= q.Life)
                {
                    Return(q.Renderer);
                    Return(q.Rim);
                    q.Renderer = null;
                    q.Rim = null;
                    q.Owner = null;
                    particles[i] = particles[particles.Count - 1];
                    particles.RemoveAt(particles.Count - 1);
                    spareParticles.Push(q);
                    continue;
                }
                float k = q.Age / Mathf.Max(q.Life, 0.0001f);
                float cell = q.Owner.CellSize;
                Palette p = q.Owner.Colours;
                // Thrown, not floated: a hard launch bleeding off, then the arc.
                q.Velocity *= Mathf.Pow(q.Drag, step);
                q.Velocity.y -= q.Gravity * cell * step;
                q.At += q.Velocity * step;
                q.Angle += q.Spin * step;
                q.Spin *= Mathf.Pow(0.3f, step);

                Color c;
                float alpha;
                float sx;
                float sy;
                float angle = q.Angle;
                switch (q.Kind)
                {
                    case Kind.Major:
                    case Kind.Secondary:
                    {
                        // Hot on the break, its own colour in flight, darker as it goes out.
                        Color hot = Color.Lerp(q.Colour, p.Hot, 0.45f);
                        c = k < 0.22f ? Color.Lerp(hot, q.Colour, k / 0.22f)
                            : Color.Lerp(q.Colour, q.Colour * 0.55f, (k - 0.22f) / 0.78f);
                        alpha = k < 0.6f ? 1f : 1f - (k - 0.6f) / 0.4f;
                        sx = q.Size * (1f - 0.25f * k * k);
                        sy = sx;
                        break;
                    }
                    case Kind.Micro:
                        c = k < 0.2f ? Color.Lerp(p.Hot, p.Glow, k / 0.2f)
                            : Color.Lerp(p.Glow, p.Char, (k - 0.2f) / 0.8f);
                        alpha = k < 0.55f ? 1f : 1f - (k - 0.55f) / 0.45f;
                        sx = q.Size * (1f - 0.3f * k);
                        sy = sx;
                        break;
                    case Kind.Speck:
                    {
                        float speed = q.Velocity.magnitude;
                        sx = q.Size + cell * Style.HotSpeckLength
                            * Mathf.Clamp01(speed / Mathf.Max(Style.HotSpeckSpeed * cell, 0.0001f));
                        sy = q.Size;
                        c = Color.Lerp(p.Speck, p.Glow, k);
                        alpha = Mathf.Pow(1f - k, 1.2f);
                        if (speed > 0.0001f)
                        {
                            angle = Mathf.Atan2(q.Velocity.y, q.Velocity.x) * Mathf.Rad2Deg;
                        }
                        break;
                    }
                    case Kind.Flare:
                        c = p.Hot;
                        alpha = (k < 0.2f ? 1f : Mathf.Pow(1f - (k - 0.2f) / 0.8f, 2f)) * q.Alpha;
                        sx = q.Size * (0.8f + 0.4f * k);
                        sy = sx;
                        break;
                    default:
                        c = Color.Lerp(p.Glow, p.Deep, k);
                        alpha = (k < 0.2f ? k / 0.2f : Mathf.Pow(1f - (k - 0.2f) / 0.8f, 1.5f))
                            * q.Alpha;
                        sx = q.Size * (0.8f + 0.35f * k);
                        sy = sx;
                        break;
                }
                c.a = Mathf.Clamp01(alpha);
                q.Renderer.color = c;
                Transform t = q.Renderer.transform;
                var position = new Vector3(q.At.x, q.At.y, 0f);
                Quaternion rotation = Quaternion.Euler(0f, 0f, angle);
                t.localPosition = position;
                t.localRotation = rotation;
                t.localScale = new Vector3(sx / q.Unit, sy / q.Unit, 1f);
                if (q.Rim != null)
                {
                    // The hot edge the break left, cooling off in the first half of the flight.
                    Color rim = p.Glow;
                    rim.a = Mathf.Clamp01(0.9f * (1f - k / 0.45f)) * c.a;
                    q.Rim.color = rim;
                    q.Rim.transform.localPosition = position;
                    q.Rim.transform.localRotation = rotation;
                    q.Rim.transform.localScale = new Vector3(sx, sy, 1f);
                }
            }
        }

        private bool OwnsParticles(Burst b)
        {
            for (int i = 0; i < particles.Count; i++)
            {
                if (particles[i].Owner == b)
                {
                    return true;
                }
            }
            return false;
        }

        // =================================================================== renderers

        private static void Place(SpriteRenderer r, Vector2 at, float size, Color colour,
            float alpha)
        {
            colour.a = Mathf.Clamp01(alpha);
            r.color = colour;
            r.transform.localPosition = new Vector3(at.x, at.y, 0f);
            r.transform.localRotation = Quaternion.identity;
            r.transform.localScale = new Vector3(size, size, 1f);
        }

        /// <summary>The built-in sprite material, taken from the first renderer made here. Every
        /// rented renderer is put back on it unless told otherwise - a pooled renderer that last
        /// wore a water tile's swirl must not swirl the dot it draws next.</summary>
        private Material plainMaterial;

        private SpriteRenderer Rent(Sprite sprite, int order, Material material = null)
        {
            SpriteRenderer r;
            if (spareRenderers.Count > 0)
            {
                r = spareRenderers.Pop();
            }
            else
            {
                var go = new GameObject("Burst");
                go.transform.SetParent(transform, false);
                r = go.AddComponent<SpriteRenderer>();
                if (plainMaterial == null)
                {
                    plainMaterial = r.sharedMaterial;
                }
            }
            Material wanted = material != null ? material : plainMaterial;
            if (wanted != null && r.sharedMaterial != wanted)
            {
                r.sharedMaterial = wanted;
            }
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
            spareRenderers.Push(r);
        }

        private void Release(Burst b)
        {
            for (int i = 0; i < b.Count; i++)
            {
                Return(b.Shell[i]);
                Return(b.Glow[i]);
                Return(b.Heat[i]);
                Return(b.Stress[i]);
                Return(b.Core[i]);
                Return(b.Ghost[i]);
                for (int c = 0; c < b.Cracks[i].Length; c++)
                {
                    Return(b.Cracks[i][c]);
                }
            }
            Return(b.GroupCore);
        }

        // =================================================================== shapes

        /// <summary>Major shell fragments: a skewed quad, a trapezoid, a broken-off corner, a thin
        /// panel and a kite. Convex, counter-clockwise, in a unit square - and angular enough that
        /// at a tenth of a cell none of them still reads as a square.</summary>
        private static readonly float[][] MajorShapes =
        {
            new[] { -0.46f, -0.30f, 0.30f, -0.48f, 0.48f, 0.24f, -0.20f, 0.44f },
            new[] { -0.50f, -0.28f, 0.50f, -0.34f, 0.22f, 0.30f, -0.30f, 0.36f },
            new[] { -0.48f, -0.48f, 0.46f, -0.36f, -0.36f, 0.46f },
            new[] { -0.50f, -0.12f, 0.44f, -0.24f, 0.50f, 0.10f, -0.40f, 0.20f },
            new[] { -0.50f, 0.02f, 0.08f, -0.40f, 0.50f, 0.06f, 0.02f, 0.30f }
        };

        /// <summary>Secondary chunks: a wedge, a five-sided chunk, a skewed quad.</summary>
        private static readonly float[][] SecondaryShapes =
        {
            new[] { -0.42f, -0.34f, 0.46f, -0.26f, -0.06f, 0.44f },
            new[] { -0.40f, -0.30f, 0.20f, -0.44f, 0.46f, 0.02f, 0.14f, 0.42f, -0.44f, 0.18f },
            new[] { -0.36f, -0.44f, 0.44f, -0.24f, 0.36f, 0.40f, -0.46f, 0.20f }
        };

        /// <summary>Micro debris: a tiny shard, a sliver, a fleck and a broken corner.</summary>
        private static readonly float[][] MicroShapes =
        {
            new[] { -0.40f, -0.30f, 0.46f, -0.12f, -0.20f, 0.42f },
            new[] { -0.48f, -0.08f, 0.40f, -0.18f, 0.48f, 0.06f, -0.40f, 0.16f },
            new[] { -0.30f, -0.40f, 0.30f, -0.36f, 0.44f, 0.10f, 0f, 0.44f, -0.42f, 0.08f },
            new[] { -0.44f, -0.44f, 0.40f, -0.40f, -0.40f, 0.40f }
        };

        /// <summary>The crack patterns, in cube units: each crack is the hot point, a kink and
        /// the rim. Turned and mirrored per cube, five patterns are forty looks.</summary>
        private static readonly float[][][] CrackPresets =
        {
            new[]
            {
                new[] { 0.04f, -0.02f, -0.18f, 0.10f, -0.44f, 0.30f },
                new[] { 0.04f, -0.02f, 0.22f, 0.02f, 0.46f, 0.18f },
                new[] { 0.04f, -0.02f, 0.10f, -0.22f, 0.06f, -0.46f }
            },
            new[]
            {
                new[] { -0.05f, 0.04f, -0.24f, -0.04f, -0.46f, -0.22f },
                new[] { -0.05f, 0.04f, 0.16f, 0.20f, 0.40f, 0.44f },
                new[] { -0.05f, 0.04f, 0.20f, -0.10f, 0.44f, -0.30f },
                new[] { -0.05f, 0.04f, -0.08f, 0.26f, -0.10f, 0.46f }
            },
            new[]
            {
                new[] { 0f, 0f, -0.22f, -0.04f, -0.46f, 0.06f },
                new[] { 0f, 0f, 0.12f, 0.24f, 0.30f, 0.46f },
                new[] { 0f, 0f, 0.18f, -0.20f, 0.34f, -0.44f }
            },
            new[]
            {
                new[] { 0.06f, 0.05f, -0.14f, -0.16f, -0.40f, -0.44f },
                new[] { 0.06f, 0.05f, 0.26f, 0.06f, 0.46f, -0.04f }
            },
            new[]
            {
                new[] { -0.03f, -0.06f, -0.20f, 0.14f, -0.44f, 0.40f },
                new[] { -0.03f, -0.06f, 0.20f, 0.08f, 0.46f, 0.30f },
                new[] { -0.03f, -0.06f, -0.12f, -0.24f, -0.30f, -0.46f },
                new[] { -0.03f, -0.06f, 0.18f, -0.22f, 0.40f, -0.40f }
            }
        };

        // =================================================================== shared art

        private static Sprite dotSprite;

        private static Sprite ringSprite;

        private static Sprite stressSprite;

        private static float stressSpriteWidth = -1f;

        private static Sprite lineSprite;

        private static Sprite flareSprite;

        private static readonly Sprite[] majorMaterial = new Sprite[MajorShapes.Length];

        private static readonly Sprite[] secondaryMaterial = new Sprite[SecondaryShapes.Length];

        private static readonly Sprite[] majorRims = new Sprite[MajorShapes.Length];

        private static readonly Sprite[] microSprites = new Sprite[MicroShapes.Length];

        private static readonly Dictionary<long, Sprite> fragments = new Dictionary<long, Sprite>();

        /// <summary>
        /// A piece of the cube's OWN face: a square of its tile around <paramref name="slot"/>,
        /// cut to an irregular polygon with Sprite.OverrideGeometry - so a fox breaks into fur and
        /// a fire block into flame, not into squares of a colour. Cached per tile, slot and shape.
        /// Falls back to a shaded procedural piece if the tile cannot be cut.
        /// </summary>
        private static Sprite FragmentSprite(Sprite tile, int slot, int shape, bool secondary)
        {
            float[] poly = secondary ? SecondaryShapes[shape] : MajorShapes[shape];
            if (tile == null || tile.texture == null)
            {
                return MaterialSprite(shape, secondary);
            }
            long key = ((long)tile.GetInstanceID() << 16) ^ ((long)slot << 8) ^ ((long)shape << 1)
                ^ (secondary ? 1L : 0L);
            Sprite cut;
            if (fragments.TryGetValue(key, out cut))
            {
                return cut != null ? cut : MaterialSprite(shape, secondary);
            }
            cut = null;
            bool cuttable = !tile.packed || (tile.packingMode != SpritePackingMode.Tight
                && tile.packingRotation == SpritePackingRotation.None);
            Rect r = tile.packed ? tile.textureRect : tile.rect;
            // Every tile's meta puts one world unit on the cube's body and the pivot on its
            // centre, so the slot is found from the pivot in body pixels.
            float ppu = tile.pixelsPerUnit;
            float size = (secondary ? 0.20f : 0.30f) * ppu;
            if (cuttable && size >= 4f && size <= r.width && size <= r.height)
            {
                Vector2 at = slot < ShellSlots.Length ? ShellSlots[slot]
                    : InnerSlots[slot - ShellSlots.Length];
                float x0 = Mathf.Clamp(r.x + tile.pivot.x + at.x * ppu - size * 0.5f, r.x, r.xMax - size);
                float y0 = Mathf.Clamp(r.y + tile.pivot.y + at.y * ppu - size * 0.5f, r.y, r.yMax - size);
                try
                {
                    cut = Sprite.Create(tile.texture, new Rect(x0, y0, size, size),
                        new Vector2(0.5f, 0.5f), ppu, 0, SpriteMeshType.FullRect);
                    int count = poly.Length / 2;
                    var vertices = new Vector2[count];
                    for (int v = 0; v < count; v++)
                    {
                        vertices[v] = new Vector2((poly[v * 2] + 0.5f) * size,
                            (poly[v * 2 + 1] + 0.5f) * size);
                    }
                    var triangles = new ushort[(count - 2) * 3];
                    for (int t = 0; t < count - 2; t++)
                    {
                        triangles[t * 3] = 0;
                        triangles[t * 3 + 1] = (ushort)(t + 1);
                        triangles[t * 3 + 2] = (ushort)(t + 2);
                    }
                    cut.OverrideGeometry(vertices, triangles);
                    cut.name = "Fragment";
                }
                catch (System.Exception)
                {
                    cut = null;
                }
            }
            fragments[key] = cut;
            return cut != null ? cut : MaterialSprite(shape, secondary);
        }

        /// <summary>The same shapes drawn procedurally, lit from the top left - for a cube with
        /// no face that can be cut.</summary>
        private static Sprite MaterialSprite(int shape, bool secondary)
        {
            Sprite[] cache = secondary ? secondaryMaterial : majorMaterial;
            if (cache[shape] == null)
            {
                cache[shape] = PolygonSprite(secondary ? SecondaryShapes[shape] : MajorShapes[shape],
                    48, false);
            }
            return cache[shape];
        }

        /// <summary>A major piece's hot edge: a thin band just inside its outline.</summary>
        private static Sprite RimSprite(int shape, bool secondary)
        {
            if (secondary)
            {
                return null;
            }
            if (majorRims[shape] == null)
            {
                majorRims[shape] = PolygonSprite(MajorShapes[shape], 48, true);
            }
            return majorRims[shape];
        }

        private static Sprite MicroSprite(int variant)
        {
            if (microSprites[variant] == null)
            {
                microSprites[variant] = PolygonSprite(MicroShapes[variant], 24, false);
            }
            return microSprites[variant];
        }

        /// <summary>A convex polygon, supersampled. Solid and shaded (light top-left, a darker
        /// broken rim) - or, for <paramref name="rim"/>, only a band just inside the outline.</summary>
        private static Sprite PolygonSprite(float[] shape, int n, bool rim)
        {
            const int ss = 3;
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    int inside = 0;
                    float depth = 0f;
                    for (int sy = 0; sy < ss; sy++)
                    {
                        for (int sx = 0; sx < ss; sx++)
                        {
                            float d = InsideConvex(shape, (x + (sx + 0.5f) / ss) / n - 0.5f,
                                (y + (sy + 0.5f) / ss) / n - 0.5f);
                            if (d >= 0f)
                            {
                                inside++;
                                depth = Mathf.Max(depth, d);
                            }
                        }
                    }
                    float cover = inside / (float)(ss * ss);
                    float grey;
                    float alpha;
                    if (rim)
                    {
                        grey = 1f;
                        alpha = cover * Mathf.Clamp01(1f - depth / 0.10f);
                    }
                    else
                    {
                        float u = (x + 0.5f) / n - 0.5f;
                        float v = (y + 0.5f) / n - 0.5f;
                        grey = Mathf.Lerp(0.55f, 1f, Mathf.Clamp01(0.5f + (v - u) * 1.1f))
                            * Mathf.Lerp(0.8f, 1f, Mathf.Clamp01(depth / 0.07f));
                        alpha = cover;
                    }
                    byte g = (byte)Mathf.RoundToInt(Mathf.Clamp01(grey) * 255f);
                    px[y * n + x] = new Color32(g, g, g, (byte)Mathf.RoundToInt(alpha * 255f));
                }
            }
            return MakeSprite(n, n, px, n);
        }

        /// <summary>Distance inside a convex polygon given counter-clockwise as x,y pairs;
        /// negative outside.</summary>
        private static float InsideConvex(float[] shape, float u, float v)
        {
            float best = float.MaxValue;
            int count = shape.Length / 2;
            for (int e = 0; e < count; e++)
            {
                float ax = shape[e * 2];
                float ay = shape[e * 2 + 1];
                float bx = shape[(e + 1) % count * 2];
                float by = shape[(e + 1) % count * 2 + 1];
                float ex = bx - ax;
                float ey = by - ay;
                float len = Mathf.Sqrt(ex * ex + ey * ey);
                best = Mathf.Min(best, (ex * (v - ay) - ey * (u - ax)) / Mathf.Max(len, 0.0001f));
            }
            return best;
        }

        /// <summary>A soft round light that reaches exactly zero at its edge.</summary>
        private static Sprite DotSprite()
        {
            if (dotSprite != null)
            {
                return dotSprite;
            }
            const int n = 64;
            float floor = Mathf.Exp(-3.6f);
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float u = ((x + 0.5f) / n - 0.5f) * 2f;
                    float v = ((y + 0.5f) / n - 0.5f) * 2f;
                    float r2 = u * u + v * v;
                    float a = r2 >= 1f ? 0f : (Mathf.Exp(-3.6f * r2) - floor) / (1f - floor);
                    px[y * n + x] = new Color32(255, 255, 255,
                        (byte)Mathf.RoundToInt(Mathf.Clamp01(a) * 255f));
                }
            }
            dotSprite = MakeSprite(n, n, px, n);
            return dotSprite;
        }

        /// <summary>A crack: a thin line with soft sides and tapered ends, one unit long.</summary>
        private static Sprite LineSprite()
        {
            if (lineSprite != null)
            {
                return lineSprite;
            }
            const int w = 64;
            const int h = 16;
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float t = (x + 0.5f) / w;
                    float dy = (y + 0.5f - h * 0.5f) / 3.5f;
                    float a = Mathf.Exp(-dy * dy) * Mathf.Clamp01(t / 0.12f)
                        * Mathf.Clamp01((1f - t) / 0.12f);
                    px[y * w + x] = new Color32(255, 255, 255,
                        (byte)Mathf.RoundToInt(Mathf.Clamp01(a) * 255f));
                }
            }
            lineSprite = MakeSprite(w, h, px, w);
            return lineSprite;
        }

        /// <summary>The accent on a few cubes: four thin tapering rays, two long and two short -
        /// a flare, not a sparkle.</summary>
        private static Sprite FlareSprite()
        {
            if (flareSprite != null)
            {
                return flareSprite;
            }
            const int n = 64;
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float u = ((x + 0.5f) / n - 0.5f) * 2f;
                    float v = ((y + 0.5f) / n - 0.5f) * 2f;
                    float a = Mathf.Exp(-(u * u + v * v) / 0.004f);
                    for (int ray = 0; ray < 4; ray++)
                    {
                        float length = ray % 2 == 0 ? 1f : 0.55f;
                        float along = ray == 0 ? u : ray == 1 ? v : ray == 2 ? -u : -v;
                        float across = ray % 2 == 0 ? Mathf.Abs(v) : Mathf.Abs(u);
                        if (along <= 0f || along >= length)
                        {
                            continue;
                        }
                        float fall = 1f - along / length;
                        float width = 0.035f * fall + 0.008f;
                        a = Mathf.Max(a, Mathf.Pow(fall, 1.5f) * Mathf.Exp(-(across / width) * (across / width)));
                    }
                    px[y * n + x] = new Color32(255, 255, 255,
                        (byte)Mathf.RoundToInt(Mathf.Clamp01(a) * 255f));
                }
            }
            flareSprite = MakeSprite(n, n, px, n);
            return flareSprite;
        }

        /// <summary>The ghost shell: the cube's outline, very thin.</summary>
        private static Sprite RingSprite()
        {
            if (ringSprite == null)
            {
                ringSprite = BuildRing(0.012f, 1f);
            }
            return ringSprite;
        }

        /// <summary>Edge stress: the same line kept only across the middle of each side, so the
        /// pressure shows on the faces without ever outlining the cube.</summary>
        private static Sprite StressSprite()
        {
            if (stressSprite != null && Mathf.Approximately(stressSpriteWidth, Style.EdgeStressWidth))
            {
                return stressSprite;
            }
            stressSprite = BuildRing(Mathf.Max(0.004f, Style.EdgeStressWidth), 0.30f);
            stressSpriteWidth = Style.EdgeStressWidth;
            return stressSprite;
        }

        private static Sprite BuildRing(float halfWidth, float reach)
        {
            const int n = 96;
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float u = (x + 0.5f) / n - 0.5f;
                    float v = (y + 0.5f) / n - 0.5f;
                    float sd = RoundedBox(u, v, 0.16f);
                    float off = Mathf.Abs(sd + 0.03f);
                    float a = off <= halfWidth
                        ? 1f
                        : Mathf.Pow(Mathf.Clamp01(1f - (off - halfWidth) / 0.04f), 2f);
                    if (sd > 0f)
                    {
                        a *= Mathf.Clamp01(1f - sd / 0.03f);
                    }
                    if (reach < 1f)
                    {
                        float along = Mathf.Abs(u) > Mathf.Abs(v) ? Mathf.Abs(v) : Mathf.Abs(u);
                        a *= Mathf.Clamp01((reach - along) / 0.08f);
                    }
                    px[y * n + x] = new Color32(255, 255, 255,
                        (byte)Mathf.RoundToInt(Mathf.Clamp01(a) * 255f));
                }
            }
            return MakeSprite(n, n, px, n);
        }

        private static Sprite MakeSprite(int w, int h, Color32[] px, float ppu)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.hideFlags = HideFlags.HideAndDontSave;
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.SetPixels32(px);
            tex.Apply(false, false);
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), ppu);
        }

        /// <summary>Signed distance to a rounded square of half-size 0.5: negative inside.</summary>
        private static float RoundedBox(float u, float v, float radius)
        {
            float dx = Mathf.Abs(u) - (0.5f - radius);
            float dy = Mathf.Abs(v) - (0.5f - radius);
            float outside = Mathf.Sqrt(Mathf.Max(dx, 0f) * Mathf.Max(dx, 0f)
                + Mathf.Max(dy, 0f) * Mathf.Max(dy, 0f));
            return outside + Mathf.Min(Mathf.Max(dx, dy), 0f) - radius;
        }
    }
}
