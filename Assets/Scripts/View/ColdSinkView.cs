// PURPOSE: "Soğuk kuyu" (Cold Sink) - one of the ways a cube a boss REMOVED leaves the board
// (TurnReport.LiftedCells whose LiftKindAt is Removed: "Alzheimer" forgetting a card, "Hidrolik
// pres" going off). GameUiController.PlayRemoval picks a removal variant at random for every
// removal; this is variant 1.
//
// THE CUBE DOES NOT BREAK, DISSOLVE OR TELEPORT. The floor of its cell gives way: the slot sinks
// into a three-step recess - the collapsing floor, a darker rounded inner bevel, a deep slate
// void (never black: black reads as a portal) - the cube loses its footing, settles into the
// mouth, and FALLS, slow at first and then giving in to it. It shrinks and darkens as it goes,
// but what sells the fall is the FRONT LIP: a SpriteMask the size of the mouth clips it, so its
// bottom goes behind the lip first, then its body, its top edge last. A few pale motes drift IN
// from the rim and drop after it - the opposite of an explosion's debris. The empty pit holds a
// moment, closes back layer by layer (void, bevel, floor, rim) and leaves a faint cold residue
// on what is by then an ordinary empty slot.
//
// WHY THE FALL IS SHORT ON SCREEN. Every mask on the board reveals every masked sprite, so a cube
// sliding far enough to reach the NEXT cell's mouth would show through that pit. FallDrop and
// FallScaleEnd are chosen so the cube's lowest edge never gets there, and so it is exactly behind
// the lip as the fall ends - Tools/UiLayoutCheck/cold_sink.py checks both.
//
// NOT AN EXPLOSION. No outward debris, no flash, no shake, no sound of its own. The cube's own face
// (BoardView.TryCubeLook) and its own material are kept until the floor goes: water swirls until
// it falls.

using System.Collections.Generic;
using UnityEngine;

namespace ProjectBlock.View
{
    /// <summary>Removal variant 1: the floor opens and the cube falls in. Fire and forget.</summary>
    public sealed class ColdSinkView : MonoBehaviour
    {
        // =================================================================== TUNING
        /// <summary>Everything that decides how the sink READS. Sizes and distances in CELLS,
        /// times in seconds.</summary>
        public static class Style
        {
            // ---- the wave ----
            /// <summary>Seconds per cell of distance from the centroid: a surface collapse
            /// running out across the group.</summary>
            public static float CenterOutDelay = 0.03f;

            /// <summary>The most the wave may take to reach the furthest cell.</summary>
            public static float CenterOutMaxSpread = 0.15f;

            /// <summary>Either way, from a hash of the cell.</summary>
            public static float DelayJitter = 0.006f;

            // ---- the floor opens ----
            public static float OpenDuration = 0.11f;

            /// <summary>How far below the mouth the deepest layer sits on screen - the parallax
            /// that makes it a recess rather than a dark square.</summary>
            public static float InnerDepth = 0.045f;

            /// <summary>The inner bevel, as a fraction of the mouth.</summary>
            public static float InnerScale = 0.78f;

            /// <summary>The deep void, as a fraction of the mouth.</summary>
            public static float VoidScale = 0.56f;

            /// <summary>How much darker the slot's own floor gets as it gives way.</summary>
            public static float FloorDarken = 0.35f;

            /// <summary>Cold light from below on the back of the mouth. Faint - never an outline.</summary>
            public static float RimStrength = 0.32f;

            public static float RimWidth = 0.012f;

            // ---- the cube lets go ----
            public static float DetachDuration = 0.08f;

            /// <summary>How far it settles as it loses its footing.</summary>
            public static float DetachDrop = 0.02f;

            /// <summary>How much of its highlight it loses.</summary>
            public static float DetachDim = 0.12f;

            public static float ShadowStrength = 0.55f;

            // ---- the fall ----
            public static float FallDuration = 0.34f;

            /// <summary>Above 1: slow at first, then giving in to it.</summary>
            public static float FallEase = 2f;

            /// <summary>The fall's scale curve: where it ends, relative to the mouth, and how
            /// it bends on the way - 1, .87, .67, .44, .18 at the quarters.</summary>
            public static float FallScaleEnd = 0.18f;

            public static float FallScaleBend = 1.3f;

            /// <summary>How far it slides down the screen: just enough to pass behind the front
            /// lip, never into the next cell's pit.</summary>
            public static float FallDrop = 0.47f;

            public static float FallDropBend = 1.6f;

            /// <summary>The fall's brightness curve: how dark it is when it is gone.</summary>
            public static float FallBrightnessEnd = 0.22f;

            /// <summary>The fall's saturation curve: how far toward cold grey it goes.</summary>
            public static float FallDesaturation = 0.35f;

            /// <summary>Degrees of tilt as it goes, the way its weight takes it. Never a spin.</summary>
            public static float FallRotationAmount = 3f;

            /// <summary>Fall time differs by up to this fraction, from a hash of the cell.</summary>
            public static float FallVariation = 0.08f;

            /// <summary>How thick the front lip is drawn over the cube it hides.</summary>
            public static float FrontLipDepth = 0.03f;

            public static float FrontLipStrength = 0.8f;

            // ---- motes ----
            public static int MoteCountMin = 3;

            public static int MoteCountMax = 6;

            /// <summary>How far in a mote gets over its life: 1 reaches the middle.</summary>
            public static float MoteSpeed = 1f;

            public static float MoteLifetime = 0.30f;

            public static float MoteSize = 0.035f;

            // ---- the empty pit, the close, what is left ----
            public static float EmptyHoldDuration = 0.11f;

            public static float CloseDuration = 0.20f;

            /// <summary>Above 1: the layers start back quickly and settle into place.</summary>
            public static float CloseEase = 2.2f;

            /// <summary>Close time differs by up to this fraction, from a hash of the cell.</summary>
            public static float CloseVariation = 0.05f;

            public static float ResidueStrength = 0.10f;

            public static float ResidueDuration = 0.20f;

            // ---- level of detail ----
            /// <summary>How far per-cell detail (motes, rim light) falls from N&lt;=5 to N&gt;20.</summary>
            public static float LargeNDetailCompensation = 0.8f;
        }

        // =================================================================== palette
        // Cold and desaturated throughout: this removal earned nothing. Nothing here is black and
        // nothing is electric blue.

        private static readonly Color BevelColour = new Color(0.066f, 0.076f, 0.100f);

        private static readonly Color VoidColour = new Color(0.030f, 0.040f, 0.062f);

        private static readonly Color RimColour = new Color(0.50f, 0.60f, 0.70f);

        private static readonly Color LipColour = new Color(0.19f, 0.21f, 0.26f);

        private static readonly Color ColdGrey = new Color(0.36f, 0.42f, 0.50f);

        private static readonly Color MoteColour = new Color(0.64f, 0.71f, 0.79f);

        private static readonly Color ResidueColour = new Color(0.22f, 0.27f, 0.34f);

        private static readonly Color ShadowColour = new Color(0.02f, 0.025f, 0.04f);

        private static readonly Color Clear = new Color(1f, 1f, 1f, 0f);

        // =================================================================== layers

        private const int FloorOrder = 5;

        private const int BevelOrder = 6;

        private const int VoidOrder = 7;

        private const int ShadowOrder = 8;

        /// <summary>The only masked thing here. The masks' range straddles it with a spare order
        /// either side, so whether the ends are inclusive does not matter - and it stays clear of
        /// the infection masks (1-4).</summary>
        private const int CubeOrder = 9;

        private const int MaskBack = 7;

        private const int MaskFront = 10;

        private const int LipOrder = 10;

        private const int MoteOrder = 11;

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

        private sealed class Pit
        {
            public Vector2 At;
            public float Start;
            public float FallTime;
            public float CloseTime;
            public float Tilt;
            public int Motes;
            public Dice Dice;
            public ClusterBurstView.Look Look;
            public bool Falling;
            public bool MotesThrown;
            public bool LateThrown;
            public SpriteRenderer Floor;
            public SpriteRenderer Bevel;
            public SpriteRenderer Void;
            public SpriteRenderer Rim;
            public SpriteRenderer Lip;
            public SpriteRenderer Shadow;
            public SpriteRenderer Cube;
            public SpriteRenderer Residue;
            public SpriteMask Mask;
        }

        private sealed class Sink
        {
            public readonly List<Pit> Pits = new List<Pit>();
            public float Clock;
            public float End;
            public float Cell;
            public float CubeSize;
            public float Mouth;
            public float Lod;
            public Color Slot;
        }

        private sealed class Mote
        {
            public Sink Owner;
            public SpriteRenderer Renderer;
            public Vector2 From;
            public Vector2 To;
            public float Size;
            public float Life;
            public float Age;
        }

        private readonly List<Sink> sinks = new List<Sink>();

        private readonly List<Mote> motes = new List<Mote>();

        private readonly Stack<Mote> spareMotes = new Stack<Mote>();

        private readonly Stack<SpriteRenderer> spareRenderers = new Stack<SpriteRenderer>();

        private readonly Stack<SpriteMask> spareMasks = new Stack<SpriteMask>();

        private Material plainMaterial;

        // =================================================================== driving it

        /// <summary>
        /// Opens a pit under each of <paramref name="cells"/> (world centres) and drops the cube
        /// in <paramref name="looks"/> into it. <paramref name="cubeSize"/> is the size the board
        /// draws a cube at and <paramref name="slotSize"/> the size of an empty slot - the mouth.
        /// </summary>
        public void Play(IReadOnlyList<Vector2> cells, IReadOnlyList<ClusterBurstView.Look> looks,
            float cellSize, float cubeSize, float slotSize)
        {
            if (cells == null || cells.Count == 0 || cellSize <= 0f)
            {
                return;
            }
            int n = cells.Count;
            var sink = new Sink
            {
                Cell = cellSize,
                CubeSize = cubeSize > 0f ? cubeSize : cellSize,
                Mouth = slotSize > 0f ? slotSize : cellSize * 0.82f,
                Slot = BoardView.EmptySlotColor,
                Lod = (n <= 5 ? 0f : n <= 12 ? 0.35f : n <= 20 ? 0.65f : 1f)
                    * Mathf.Clamp01(Style.LargeNDetailCompensation)
            };

            Vector2 centre = Vector2.zero;
            for (int i = 0; i < n; i++)
            {
                centre += cells[i];
            }
            centre /= n;
            float furthest = 0f;
            for (int i = 0; i < n; i++)
            {
                furthest = Mathf.Max(furthest, (cells[i] - centre).magnitude);
            }
            float furthestCells = furthest / cellSize;
            float perCell = furthestCells > 0f
                ? Mathf.Min(Style.CenterOutDelay, Style.CenterOutMaxSpread / furthestCells) : 0f;
            Sprite fallbackTile = ViewUtil.DefaultTile != null ? ViewUtil.DefaultTile
                : ViewUtil.WhiteSprite;

            for (int i = 0; i < n; i++)
            {
                uint seed = Hash(Mathf.RoundToInt(cells[i].x / cellSize * 4f),
                    Mathf.RoundToInt(cells[i].y / cellSize * 4f));
                var dice = new Dice(seed);
                var pit = new Pit { At = cells[i], Dice = dice };
                float jitter = n > 1 ? pit.Dice.Range(-1f, 1f) * Style.DelayJitter : 0f;
                pit.Start = Mathf.Max(0f, (cells[i] - centre).magnitude / cellSize * perCell + jitter);
                pit.FallTime = Style.FallDuration * (1f + Style.FallVariation * pit.Dice.Range(-1f, 1f));
                pit.CloseTime = Style.CloseDuration * (1f + Style.CloseVariation * pit.Dice.Range(-1f, 1f));
                pit.Tilt = pit.Dice.Next() < 0.5f ? -1f : 1f;
                int full = Mathf.RoundToInt(Mathf.Lerp(Style.MoteCountMax, Style.MoteCountMin * 0.5f,
                    sink.Lod));
                pit.Motes = Mathf.Max(1, full + Mathf.RoundToInt(pit.Dice.Range(-1f, 1f)));
                bool known = looks != null && i < looks.Count && looks[i].Tile != null;
                pit.Look = known ? looks[i] : new ClusterBurstView.Look
                {
                    Tile = fallbackTile,
                    Colour = new Color(0.62f, 0.68f, 0.82f)
                };

                pit.Floor = Rent(ViewUtil.WhiteSprite, FloorOrder, null);
                // Over the floor, not beside it: it has to show the moment the floor is back.
                pit.Residue = Rent(SoftSquareSprite(), BevelOrder, null);
                pit.Bevel = Rent(BevelSprite(), BevelOrder, null);
                pit.Void = Rent(SoftSquareSprite(), VoidOrder, null);
                pit.Shadow = Rent(SoftSquareSprite(), ShadowOrder, null);
                // The cube wears its tile's own material until the floor goes.
                pit.Cube = Rent(pit.Look.Tile, CubeOrder, ViewUtil.TileMaterial(pit.Look.Tile));
                pit.Rim = Rent(RimSprite(), LipOrder, null);
                pit.Lip = Rent(LipSprite(), LipOrder, null);
                // On THIS frame: the board repainted the cell empty a moment ago.
                Place(pit.Cube, cells[i], sink.CubeSize, pit.Look.Colour, 1f, 0f);
                sink.Pits.Add(pit);

                float end = pit.Start + DetachStart() + Style.DetachDuration + pit.FallTime
                    + Style.EmptyHoldDuration + pit.CloseTime + Style.ResidueDuration;
                sink.End = Mathf.Max(sink.End, end);
            }
            sinks.Add(sink);
        }

        /// <summary>Stops everything at once - the run ending, the board being torn down.</summary>
        public void Stop()
        {
            for (int i = 0; i < sinks.Count; i++)
            {
                Release(sinks[i]);
            }
            sinks.Clear();
            for (int i = 0; i < motes.Count; i++)
            {
                Return(motes[i].Renderer);
                spareMotes.Push(motes[i]);
            }
            motes.Clear();
        }

        /// <summary>The cube starts to let go before the floor has finished opening: the floor
        /// giving way is what takes its footing.</summary>
        private static float DetachStart()
        {
            return Style.OpenDuration * 0.7f;
        }

        private static float CloseCurve(float k)
        {
            k = Mathf.Clamp01(k);
            return 1f - Mathf.Pow(1f - k, Mathf.Max(Style.CloseEase, 0.05f));
        }

        // =================================================================== the clock

        private void Update()
        {
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            for (int s = sinks.Count - 1; s >= 0; s--)
            {
                Sink sink = sinks[s];
                sink.Clock += dt;
                for (int i = 0; i < sink.Pits.Count; i++)
                {
                    PaintPit(sink, sink.Pits[i]);
                }
                if (sink.Clock >= sink.End && !OwnsMotes(sink))
                {
                    Release(sink);
                    sinks.RemoveAt(s);
                }
            }
            UpdateMotes(dt);
        }

        /// <summary>One pit at its own age: opening, holding its cube, empty, closing, gone.</summary>
        private void PaintPit(Sink sink, Pit pit)
        {
            float t = sink.Clock - pit.Start;
            float cell = sink.Cell;
            float mouth = sink.Mouth;
            float detachStart = DetachStart();
            float fallStart = detachStart + Style.DetachDuration;
            float fallEnd = fallStart + pit.FallTime;
            float closeStart = fallEnd + Style.EmptyHoldDuration;
            float closeEnd = closeStart + pit.CloseTime;
            float residueEnd = closeEnd + Style.ResidueDuration;

            // How open each layer is. They open together and close back the way they came: the
            // void first, then the bevel, then the floor - a floor settling, not a ring shutting.
            float floor;
            float bevel;
            float deep;
            float rim;
            if (t < 0f || t >= closeEnd)
            {
                floor = 0f;
                bevel = 0f;
                deep = 0f;
                rim = 0f;
            }
            else if (t < Style.OpenDuration)
            {
                float k = t / Mathf.Max(Style.OpenDuration, 0.0001f);
                floor = 1f - (1f - k) * (1f - k);
                bevel = floor;
                deep = floor;
                rim = floor;
            }
            else if (t < closeStart)
            {
                floor = 1f;
                bevel = 1f;
                deep = 1f;
                // The rim settles while the pit stands empty.
                rim = t < fallEnd ? 1f
                    : Mathf.Lerp(1f, 0.6f, (t - fallEnd) / Mathf.Max(Style.EmptyHoldDuration, 0.0001f));
            }
            else
            {
                float k = (t - closeStart) / Mathf.Max(pit.CloseTime, 0.0001f);
                deep = 1f - CloseCurve(k / 0.6f);
                bevel = 1f - CloseCurve((k - 0.2f) / 0.6f);
                floor = 1f - CloseCurve((k - 0.4f) / 0.6f);
                rim = 0.6f * (1f - Mathf.Clamp01(k / 0.7f));
            }

            Vector2 at = pit.At;
            // The slot's own floor giving way: flat, exactly the slot, so it hands the cell back
            // without a seam.
            if (floor > 0f)
            {
                Place(pit.Floor, at, mouth, Color.Lerp(sink.Slot, sink.Slot * (1f - Style.FloorDarken),
                    floor), 1f, 0f);
            }
            else
            {
                pit.Floor.color = Clear;
            }
            // The rounded step inside, and the deep void inside that - each a little lower on
            // screen than the one above it: that offset is the depth.
            Place(pit.Bevel, at + Vector2.down * (Style.InnerDepth * 0.45f * bevel * cell),
                mouth * Mathf.Lerp(1f, Style.InnerScale, bevel), BevelColour, bevel, 0f);
            float open = Mathf.Max(deep, 0f);
            Place(pit.Void, at + Vector2.down * (Style.InnerDepth * open * cell),
                mouth * Style.VoidScale * Mathf.Pow(open, 0.7f), VoidColour, Mathf.Sqrt(open), 0f);
            // Cold light from below on the back of the mouth, and the front lip in front of it all.
            Place(pit.Rim, at, mouth, RimColour, Style.RimStrength * (1f - 0.4f * sink.Lod) * rim, 0f);
            float lip = Style.FrontLipDepth * cell;
            PlaceRect(pit.Lip, at + new Vector2(0f, -mouth * 0.5f + lip * 0.5f), mouth, lip, LipColour,
                Style.FrontLipStrength * bevel);

            PaintCube(sink, pit, t, detachStart, fallStart, fallEnd);

            // What is left: a faint cold smear on what is, by now, an ordinary empty slot.
            float residue = 0f;
            float from = closeEnd - pit.CloseTime * 0.3f;
            if (t >= from && t < residueEnd)
            {
                float rise = Mathf.Clamp01((t - from) / Mathf.Max(pit.CloseTime * 0.3f, 0.0001f));
                float fade = t > closeEnd
                    ? 1f - (t - closeEnd) / Mathf.Max(Style.ResidueDuration, 0.0001f) : 1f;
                residue = Style.ResidueStrength * rise * Mathf.Pow(Mathf.Clamp01(fade), 1.5f);
            }
            Place(pit.Residue, at, mouth, ResidueColour, residue, 0f);

            if (!pit.MotesThrown && t >= detachStart)
            {
                pit.MotesThrown = true;
                ThrowMotes(sink, pit, pit.Motes);
            }
            if (!pit.LateThrown && t >= fallEnd)
            {
                pit.LateThrown = true;
                if (sink.Lod < 0.5f)
                {
                    ThrowMotes(sink, pit, pit.Dice.Next() < 0.5f ? 1 : 2);
                }
            }
        }

        /// <summary>The cube: standing, letting go, falling behind the lip.</summary>
        private void PaintCube(Sink sink, Pit pit, float t, float detachStart, float fallStart,
            float fallEnd)
        {
            float cell = sink.Cell;
            float mouth = sink.Mouth;
            Color face = pit.Look.Colour;
            face.a = 1f;
            if (t >= fallEnd)
            {
                pit.Cube.color = Clear;
                pit.Shadow.color = Clear;
                ReleaseMask(pit);
                return;
            }
            if (t < fallStart)
            {
                // Standing, then letting go: it settles into the mouth, loses a little of its
                // highlight, and its contact shadow tightens. Its own face and material throughout.
                float k = t < detachStart ? 0f
                    : Mathf.Clamp01((t - detachStart) / Mathf.Max(Style.DetachDuration, 0.0001f));
                float settle = 1f - (1f - k) * (1f - k);
                float size = Mathf.Lerp(sink.CubeSize, mouth, settle);
                Vector2 at = pit.At + Vector2.down * (Style.DetachDrop * cell * settle);
                Place(pit.Cube, at, size, face * (1f - Style.DetachDim * settle), 1f, 0f);
                float opening = Mathf.Clamp01(t / Mathf.Max(Style.OpenDuration, 0.0001f));
                Place(pit.Shadow, at, size * 1.06f, ShadowColour,
                    Style.ShadowStrength * Mathf.Lerp(0.4f * opening, 1f, settle), 0f);
                return;
            }
            if (!pit.Falling)
            {
                // The moment it drops below the floor: off its own animated material (a mask
                // needs the plain one) and behind the front lip from here on.
                pit.Falling = true;
                if (plainMaterial != null)
                {
                    pit.Cube.sharedMaterial = plainMaterial;
                }
                pit.Cube.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
                pit.Shadow.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
                pit.Mask = RentMask(pit.At, mouth);
            }
            // Slow at first, then giving in to it. It shrinks and slides down the screen, and the
            // mask - the mouth - cuts it off from the bottom up as it passes behind the lip.
            float u = Mathf.Clamp01((t - fallStart) / Mathf.Max(pit.FallTime, 0.0001f));
            float e = Mathf.Pow(u, Style.FallEase);
            float s = mouth * (1f - (1f - Style.FallScaleEnd) * Mathf.Pow(e, Style.FallScaleBend));
            float drop = (Style.DetachDrop + Style.FallDrop * Mathf.Pow(e, Style.FallDropBend)) * cell;
            Vector2 pos = pit.At + Vector2.down * drop;
            float bright = Mathf.Lerp(1f, Style.FallBrightnessEnd, Mathf.Pow(e, 0.8f));
            Color c = Color.Lerp(face * ((1f - Style.DetachDim) * bright), ColdGrey * bright,
                Style.FallDesaturation * e);
            // The last stretch is the fastest, so at 60fps the final frame can still show a sliver
            // above the lip; by then it is dark enough to go out into the void rather than pop.
            float gone = e < 0.7f ? 1f : 1f - (e - 0.7f) / 0.3f;
            Place(pit.Cube, pos, s, c, gone, pit.Tilt * Style.FallRotationAmount * e);
            Place(pit.Shadow, pos, s * 1.04f, ShadowColour,
                Style.ShadowStrength * Mathf.Pow(1f - e, 1.5f) * gone, 0f);
        }

        // =================================================================== motes

        /// <summary>A few pale specks at the rim of the mouth, drawn IN and down after the cube -
        /// the one thing that tells this apart from an explosion at a glance.</summary>
        private void ThrowMotes(Sink sink, Pit pit, int count)
        {
            float half = sink.Mouth * 0.46f;
            for (int m = 0; m < count; m++)
            {
                float along = pit.Dice.Range(-1f, 1f) * half;
                int side = Mathf.Min(3, (int)(pit.Dice.Next() * 4f));
                Vector2 offset = side == 0 ? new Vector2(along, half)
                    : side == 1 ? new Vector2(half, along)
                    : side == 2 ? new Vector2(along, -half)
                    : new Vector2(-half, along);
                Mote q = spareMotes.Count > 0 ? spareMotes.Pop() : new Mote();
                q.Owner = sink;
                q.From = pit.At + offset;
                q.To = pit.At + Vector2.down * (Style.InnerDepth * sink.Cell);
                q.Size = sink.Cell * Style.MoteSize * pit.Dice.Range(0.7f, 1.3f);
                q.Life = Style.MoteLifetime * pit.Dice.Range(0.8f, 1.2f);
                q.Age = -pit.Dice.Range(0f, 0.06f);
                q.Renderer = Rent(DotSprite(), MoteOrder, null);
                motes.Add(q);
            }
        }

        private void UpdateMotes(float dt)
        {
            for (int i = motes.Count - 1; i >= 0; i--)
            {
                Mote q = motes[i];
                q.Age += dt;
                if (q.Age >= q.Life)
                {
                    Return(q.Renderer);
                    q.Renderer = null;
                    q.Owner = null;
                    motes[i] = motes[motes.Count - 1];
                    motes.RemoveAt(motes.Count - 1);
                    spareMotes.Push(q);
                    continue;
                }
                if (q.Age < 0f)
                {
                    q.Renderer.color = Clear;
                    continue;
                }
                float k = q.Age / Mathf.Max(q.Life, 0.0001f);
                // Slow off the rim, gathering pace toward the middle - drawn, not blown.
                float p = Mathf.Min(1f, Mathf.Pow(k, 1.7f) * Style.MoteSpeed);
                float a = (k < 0.15f ? k / 0.15f : k > 0.7f ? (1f - k) / 0.3f : 1f) * 0.75f;
                Place(q.Renderer, Vector2.Lerp(q.From, q.To, p), q.Size * (1f - 0.7f * k), MoteColour,
                    a, 0f);
            }
        }

        private bool OwnsMotes(Sink sink)
        {
            for (int i = 0; i < motes.Count; i++)
            {
                if (motes[i].Owner == sink)
                {
                    return true;
                }
            }
            return false;
        }

        // =================================================================== renderers

        private static void Place(SpriteRenderer r, Vector2 at, float size, Color colour, float alpha,
            float angle)
        {
            colour.a = Mathf.Clamp01(alpha);
            r.color = colour;
            r.transform.localPosition = new Vector3(at.x, at.y, 0f);
            r.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
            r.transform.localScale = new Vector3(size, size, 1f);
        }

        /// <summary>For a sprite that is not square: sizes it to <paramref name="width"/> by
        /// <paramref name="height"/> whatever its own proportions.</summary>
        private static void PlaceRect(SpriteRenderer r, Vector2 at, float width, float height,
            Color colour, float alpha)
        {
            colour.a = Mathf.Clamp01(alpha);
            r.color = colour;
            Vector2 unit = r.sprite != null
                ? new Vector2(r.sprite.rect.width, r.sprite.rect.height) / r.sprite.pixelsPerUnit
                : Vector2.one;
            r.transform.localPosition = new Vector3(at.x, at.y, 0f);
            r.transform.localRotation = Quaternion.identity;
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
                var go = new GameObject("Sink");
                go.transform.SetParent(transform, false);
                r = go.AddComponent<SpriteRenderer>();
                if (plainMaterial == null)
                {
                    plainMaterial = r.sharedMaterial;
                }
            }
            // A pooled renderer may have last worn a water tile's material and a mask: both go.
            Material wanted = material != null ? material : plainMaterial;
            if (wanted != null && r.sharedMaterial != wanted)
            {
                r.sharedMaterial = wanted;
            }
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
            spareRenderers.Push(r);
        }

        /// <summary>The mouth, as a mask: the cube is visible only inside it once it falls.</summary>
        private SpriteMask RentMask(Vector2 at, float size)
        {
            SpriteMask m;
            if (spareMasks.Count > 0)
            {
                m = spareMasks.Pop();
            }
            else
            {
                var go = new GameObject("SinkMouth");
                go.transform.SetParent(transform, false);
                m = go.AddComponent<SpriteMask>();
                m.sprite = ViewUtil.WhiteSprite;
                m.isCustomRangeActive = true;
                m.backSortingOrder = MaskBack;
                m.frontSortingOrder = MaskFront;
            }
            m.transform.localPosition = new Vector3(at.x, at.y, 0f);
            m.transform.localScale = new Vector3(size, size, 1f);
            m.enabled = true;
            return m;
        }

        private void ReleaseMask(Pit pit)
        {
            if (pit.Mask == null)
            {
                return;
            }
            pit.Mask.enabled = false;
            spareMasks.Push(pit.Mask);
            pit.Mask = null;
        }

        private void Release(Sink sink)
        {
            for (int i = 0; i < sink.Pits.Count; i++)
            {
                Pit pit = sink.Pits[i];
                Return(pit.Floor);
                Return(pit.Residue);
                Return(pit.Bevel);
                Return(pit.Void);
                Return(pit.Shadow);
                Return(pit.Cube);
                Return(pit.Rim);
                Return(pit.Lip);
                ReleaseMask(pit);
            }
        }

        // =================================================================== shared art

        private static Sprite softSquareSprite;

        private static Sprite bevelSprite;

        private static Sprite rimSprite;

        private static float rimSpriteWidth = -1f;

        private static Sprite lipSprite;

        private static Sprite dotSprite;

        /// <summary>A rounded square whose edge softens inward - the void, the contact shadow and
        /// the residue: dark, never a hard black tile.</summary>
        private static Sprite SoftSquareSprite()
        {
            if (softSquareSprite != null)
            {
                return softSquareSprite;
            }
            const int n = 64;
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float sd = RoundedBox((x + 0.5f) / n - 0.5f, (y + 0.5f) / n - 0.5f, 0.2f);
                    float a = Mathf.Clamp01(-sd / 0.12f);
                    px[y * n + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            }
            softSquareSprite = MakeSprite(n, n, px, n);
            return softSquareSprite;
        }

        /// <summary>The inner step: a rounded square, crisp at its edge, its top lighter than its
        /// bottom - the back wall catching light from below, the front one in its own shadow.</summary>
        private static Sprite BevelSprite()
        {
            if (bevelSprite != null)
            {
                return bevelSprite;
            }
            const int n = 64;
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float v = (y + 0.5f) / n - 0.5f;
                    float sd = RoundedBox((x + 0.5f) / n - 0.5f, v, 0.18f);
                    float a = Mathf.Clamp01(0.5f - sd * n);
                    byte g = (byte)Mathf.RoundToInt(Mathf.Lerp(0.72f, 1f, v + 0.5f) * 255f);
                    px[y * n + x] = new Color32(g, g, g, (byte)Mathf.RoundToInt(a * 255f));
                }
            }
            bevelSprite = MakeSprite(n, n, px, n);
            return bevelSprite;
        }

        /// <summary>Cold light from below on the mouth's back edge: a thin line round the mouth,
        /// there only across its top half. Rebuilt if RimWidth is tuned while the game runs.</summary>
        private static Sprite RimSprite()
        {
            if (rimSprite != null && Mathf.Approximately(rimSpriteWidth, Style.RimWidth))
            {
                return rimSprite;
            }
            const int n = 96;
            float halfWidth = Mathf.Max(0.003f, Style.RimWidth);
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float v = (y + 0.5f) / n - 0.5f;
                    float sd = RoundedBox((x + 0.5f) / n - 0.5f, v, 0.06f);
                    float off = Mathf.Abs(sd + halfWidth * 1.5f);
                    float a = off <= halfWidth ? 1f
                        : Mathf.Pow(Mathf.Clamp01(1f - (off - halfWidth) / 0.03f), 2f);
                    if (sd > 0f)
                    {
                        a *= Mathf.Clamp01(1f - sd / 0.02f);
                    }
                    a *= Mathf.Clamp01(0.35f + v * 1.4f);
                    px[y * n + x] = new Color32(255, 255, 255,
                        (byte)Mathf.RoundToInt(Mathf.Clamp01(a) * 255f));
                }
            }
            rimSprite = MakeSprite(n, n, px, n);
            rimSpriteWidth = Style.RimWidth;
            return rimSprite;
        }

        /// <summary>The front lip: a band along the bottom of the mouth, brightest near its top -
        /// the lip's face, drawn over the cube it hides.</summary>
        private static Sprite LipSprite()
        {
            if (lipSprite != null)
            {
                return lipSprite;
            }
            const int w = 64;
            const int h = 16;
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float t = (x + 0.5f) / w;
                    float yn = (y + 0.5f) / h;
                    float a = Mathf.Clamp01(1f - Mathf.Abs(yn - 0.7f) / 0.7f)
                        * Mathf.Clamp01(t / 0.08f) * Mathf.Clamp01((1f - t) / 0.08f);
                    px[y * w + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            }
            lipSprite = MakeSprite(w, h, px, w);
            return lipSprite;
        }

        private static Sprite DotSprite()
        {
            if (dotSprite != null)
            {
                return dotSprite;
            }
            const int n = 32;
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
