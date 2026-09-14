// PURPOSE: "Yılan"'s VFX layer - the other half of the same physical event. SnakeView owns the
// snake's POSES (where every segment is, which piece it is drawn with, how it is squeezed); this
// owns everything that makes those poses read as a living thing doing something in a place:
//
//   BODY VFX     the material response on the snake's own skin (Resources/Shaders/SnakeSkin):
//                a broad sheen where the art is already lit, a leading edge that lifts and a
//                trailing one that deepens, the cream and amber accents coming up a few percent,
//                one soft band travelling along the body, and the colour draining out of a dormant
//                or dying segment. One material, twenty segments, a MaterialPropertyBlock each.
//   CONTACT VFX  what the board feels: a contact shadow under every segment (stretched along the
//                body, smaller under the tail, tight at rest and softer while it moves), a short
//                pressure oval where the head lands, a smear where it slid, a pinch at the tail
//                root, a ring where it shoved something it could not pass.
//   ACTION VFX   the two hero moments and the ends: the bite's contact arcs, the ingestion streaks
//                in the eaten block's OWN colour, the flecks a cut throws, the motes a defeat
//                releases, and one small warm pulse on the board when the boss is finally gone.
//
// INTENSITY IS A HIERARCHY, not a constant: idle and move carry no particles at all, spawn a
// handful, a bite and a cut are the loud ones, and the budget never grows with the snake's length -
// twenty segments each throwing their own effect is noise, so the middle of a long body is quiet
// and the head, the neck, the corners and the tail keep full quality.
//
// NEVER: a glow, an outline, an additive halo, neon or toxic anything, a movement trail, speed
// lines, smoke, a poison cloud, stars, comic icons, a white flash, a full-board flash, screen shake
// for an ordinary action, confetti, plain square particles, or an explosion for a bite, a cut or a
// defeat. Every effect here is local, short, and made of the snake's own three colours: teal,
// cream and a warm amber.

using System.Collections.Generic;
using ProjectBlock.Core;
using UnityEngine;

namespace ProjectBlock.View
{
    /// <summary>"Yılan"'s surface, contact and action effects.</summary>
    public sealed class SnakeVfxController : MonoBehaviour
    {
        // =================================================================== TUNING
        /// <summary>Everything the VFX layer reads. Sizes in CELLS, times in seconds. Every
        /// strength is a share of an effect that is already deliberately small: this is a premium
        /// puzzle board, not a fireworks display.</summary>
        public static class Style
        {
            // ---- common ----
            /// <summary>One knob over the whole layer, for taste and for testing.</summary>
            public static float VfxGlobalIntensity = 1f;

            /// <summary>The broad lift on a lit surface. The snake is glossy, not wet.</summary>
            public static float SurfaceSheenStrength = 0.30f;

            /// <summary>0 is a tight highlight, 1 is a very broad one.</summary>
            public static float SurfaceSheenSoftness = 0.65f;

            public static float LeadingEdgeHighlightStrength = 0.35f;

            // ---- contact shadow ----
            public static float ShadowOpacity = 0.28f;

            public static float ShadowSoftness = 0.55f;

            /// <summary>How far the shadow lets go while the snake is moving.</summary>
            public static float ShadowMoveSoftening = 0.45f;

            // ---- what the board feels ----
            public static float BoardContactStrength = 0.22f;

            public static float BoardContactDuration = 0.10f;

            /// <summary>The small warm light an action throws on the board around it.</summary>
            public static float LocalLightStrength = 0.22f;

            /// <summary>In cells. Small: this lights a cell, not half the arena.</summary>
            public static float LocalLightRadius = 1.1f;

            /// <summary>How far BELOW the contact the light pools. A light centred on the head
            /// is a halo; light has to land on something, and what it lands on is the floor.</summary>
            public static float LocalLightGroundDrop = 0.17f;

            /// <summary>How flat that pool is - it lies on the board, so it is an ellipse seen
            /// at the board's own angle, not a disc.</summary>
            public static float LocalLightGroundFlatten = 0.5f;

            // ---- waking up ----
            public static float WakeSheenStrength = 0.55f;

            public static float WakeSheenDuration = 0.22f;

            /// <summary>How much of its colour a dormant segment has lost.</summary>
            public static float WakeColorRecovery = 0.45f;

            /// <summary>How much wider and fainter a dormant segment's shadow is.</summary>
            public static float WakeShadowTightening = 0.6f;

            public static float WakeBoardPressure = 0.16f;

            /// <summary>Over the WHOLE wake, not per segment.</summary>
            public static int WakeMoteCount = 3;

            // ---- moving ----
            /// <summary>The surface flow that runs head to tail while it slides.</summary>
            public static float MoveSurfaceFlowStrength = 0.30f;

            public static float MoveLeadingHighlight = 0.40f;

            /// <summary>The mark the body's weight leaves where it slid. Not a trail: it is gone
            /// in a tenth of a second.</summary>
            public static float MoveContactSmearStrength = 0.14f;

            public static float MoveContactSmearDuration = 0.09f;

            /// <summary>The lift on the outer side of a turn, which is what gives it volume.</summary>
            public static float MoveTurnHighlightStrength = 0.55f;

            public static float MoveSettlePressureStrength = 0.20f;

            // ---- shoving at something it cannot pass ----
            public static float StuckContactPressure = 0.26f;

            public static float StuckContactDuration = 0.12f;

            public static int StuckDustCount = 2;

            public static float StuckSurfaceCompressionResponse = 0.5f;

            // ---- the bite ----
            /// <summary>Two small curved arcs where the mouth meets the block.</summary>
            public static float BiteContactArcStrength = 0.62f;

            /// <summary>How hard the head's OWN surface answers the frame the jaw lands: the
            /// hit is on the creature, so the creature is what has to show it.</summary>
            public static float BiteContactSurfaceResponse = 0.55f;

            /// <summary>The warm accent that runs back through the head and into the neck as it
            /// closes on the block.</summary>
            public static float BiteContactAccentBoost = 0.42f;

            public static float BiteContactArcDuration = 0.11f;

            public static int BiteFleckCount = 4;

            public static float BiteFleckSpeed = 0.9f;

            public static float BiteFleckLifetime = 0.30f;

            /// <summary>How much deeper the shadow round the mouth goes as it swallows.</summary>
            public static float BiteMouthShadowStrength = 0.35f;

            public static float GulpSurfaceResponse = 0.70f;

            public static float GulpAccentBoost = 0.45f;

            /// <summary>How taut the swollen segment's surface reads while it holds.</summary>
            public static float SwollenSurfaceTension = 0.55f;

            public static float GrowthContactPressure = 0.22f;

            // ---- the cut ----
            public static float CutImpactSurfaceResponse = 0.75f;

            public static int CutImpactFleckCount = 5;

            public static float CutSignalMaterialStrength = 0.62f;

            public static float CutSignalAccentBoost = 0.42f;

            public static float TailPinchShadowStrength = 0.55f;

            public static int TailDetachFleckCount = 5;

            public static float TailDetachFleckDistance = 0.32f;

            /// <summary>A breath of the tail where it was, for as long as it takes to notice it
            /// moved. Not motion blur.</summary>
            public static float TailDetachAfterimageStrength = 0.28f;

            public static float NewTailSurfaceResponse = 0.40f;

            public static float NewTailContactPressure = 0.18f;

            // ---- the defeat ----
            public static float DefeatColorDrain = 0.65f;

            public static float DefeatSurfaceDim = 0.45f;

            public static int DefeatMoteCount = 10;

            public static float DefeatMoteDistance = 0.45f;

            public static float DefeatMoteLifetime = 0.55f;

            /// <summary>The faint warmth left on the cell it died in.</summary>
            public static float DefeatResidueStrength = 0.18f;

            public static float DefeatResidueDuration = 0.12f;

            public static float DefeatBoardPulseStrength = 0.14f;

            /// <summary>In cells: one or two, never the board.</summary>
            public static float DefeatBoardPulseRadius = 1.8f;

            public static float DefeatBoardPulseDuration = 0.34f;

            /// <summary>The shadow outlives the snake by a breath and then shrinks away.</summary>
            public static float DefeatShadowDeathDuration = 0.09f;

            // ---- level of detail ----
            /// <summary>Past this length the middle of the body stops carrying its own contact
            /// effects; the head, the neck, the corners and the tail never lose theirs.</summary>
            public static int LodLongLength = 15;

            public static float LodLongContact = 0.45f;

            /// <summary>The most flecks any one event may throw, whatever its own count says.</summary>
            public static int FleckBudget = 12;
        }

        /// <summary>The lab's VFX switches, one row: what each layer contributes, on its own.</summary>
        public static class Layers
        {
            public static bool ShowSnakeShadows = true;

            public static bool ShowSnakeMaterialFx = true;

            public static bool ShowSnakeParticles = true;

            public static bool ShowSnakeBoardContact = true;

            public static void AllOn()
            {
                ShowSnakeShadows = true;
                ShowSnakeMaterialFx = true;
                ShowSnakeParticles = true;
                ShowSnakeBoardContact = true;
            }
        }

        // =================================================================== palette
        //
        // The snake's own three: its teal, its cream belly, a warm amber accent. Plus the neutral
        // the BOARD answers in - a contact mark is the board's shadow, not snake-coloured paint.

        private static readonly Color Teal = new Color(0.36f, 0.78f, 0.66f);

        private static readonly Color Cream = new Color(0.95f, 0.89f, 0.74f);

        private static readonly Color Amber = new Color(0.93f, 0.71f, 0.38f);

        /// <summary>What every contact mark on the board is drawn in: cool neutral shadow.</summary>
        private static readonly Color ContactColour = new Color(0.05f, 0.07f, 0.09f);

        private static readonly Color ShadowColour = new Color(0.02f, 0.03f, 0.035f);

        private static readonly Color Clear = new Color(1f, 1f, 1f, 0f);

        // =================================================================== layers
        //
        // Under the snake: what the board feels. Over it: only the flecks a hero action throws.

        private const int LightOrder = 1;

        private const int BoardOrder = 2;

        private const int ShadowOrder = 3;

        // A CONTACT mark is not something the board feels - it happens ON the meeting of two
        // things, so it goes over the body and the head. An arc drawn behind the jaw that makes
        // it is an arc nobody ever sees, which is exactly how the first pass lost its bite.
        private const int ContactOrder = 7;

        private const int FleckOrder = 8;

        // =================================================================== what it is handed

        /// <summary>Which piece a pose is drawn with - the VFX answers each one differently.</summary>
        public enum Part
        {
            Head,
            Body,
            Bend,
            Tail,
            Swollen
        }

        /// <summary>One segment, as SnakeView has just placed it. Everything the VFX layer needs to
        /// answer it, and nothing it could use to second-guess the rules.</summary>
        public struct SegmentPose
        {
            public SpriteRenderer Renderer;
            public Vector2 At;
            public float Width;
            public float Height;
            public Part Piece;
            public int Index;
            public int Count;
            /// <summary>The way the body runs here, pointing toward the head.</summary>
            public Vector2 Facing;
            /// <summary>How hard this segment is being squeezed right now, 0..1 of the most it is.</summary>
            public float Compression;
            /// <summary>How much of a leading edge it should carry, 0..1.</summary>
            public float Lead;
            /// <summary>Extra sheen on top of the standing amount - a wake, a turn, a gulp.</summary>
            public float Sheen;
            /// <summary>How far the cream and amber are brought up.</summary>
            public float Accent;

            /// <summary>What colour that accent is. The surface's own warm amber by default; the
            /// colour of what was just eaten while a gulp is passing through this segment, so a
            /// blue block and a gold one do not swallow the same.</summary>
            public Color AccentColour;

            /// <summary>Up to three of the colours the snake SWALLOWED, showing through from
            /// under its own surface as it is beaten - each one a colour and a place (xy in the
            /// piece's own space, z how wide, w how strongly). Only the defeat uses them.</summary>
            public Color Pocket0;

            public Vector4 PocketAt0;

            public Color Pocket1;

            public Vector4 PocketAt1;

            public Color Pocket2;

            public Vector4 PocketAt2;
            /// <summary>0 is its own colour, 1 is drained of it.</summary>
            public float Drain;
            public float Dim;
            /// <summary>A band travelling along the body: -1 for none, else 0 (tail) .. 1 (head).</summary>
            public float Band;
            public float BandStrength;
            public float Alpha;

            /// <summary>How much of the contact shadow is left, apart from the sprite's own
            /// alpha. The mass goes on its own curve as the snake is beaten, and a shadow still
            /// sitting under a body that has nearly gone gives the whole thing away.</summary>
            public float ShadowFade;
            public bool Moving;
        }

        /// <summary>Everything the snake can do that the VFX layer answers. SnakeView calls these
        /// as the motion reaches each beat, so the two are one event and not two systems.</summary>
        public enum Hook
        {
            WakeSegment,
            WakeHead,
            MoveStart,
            MoveTurn,
            MoveSettle,
            StuckPressure,
            MouthOpen,
            BiteContact,
            GulpPass,
            GrowthSettle,
            LineSnakeImpact,
            CutSignalPass,
            TailPinch,
            TailDetach,
            NewTailSettled,
            DefeatStart,
            DefeatCollapse,
            DefeatMote,
            DefeatComplete
        }

        // =================================================================== state

        private BoardView view;

        private float cellSize;

        private int length;

        private int shadowsUsed;

        /// <summary>Where the head last left a mark, so a slide leaves a few and not a line.</summary>
        private Vector2 lastSmear;

        private bool smearing;

        private enum Mark
        {
            /// <summary>A soft pressure oval on a cell.</summary>
            Oval,
            /// <summary>The ring of a shove that went nowhere.</summary>
            Ring,
            /// <summary>The mark a sliding body leaves, stretched along its own axis.</summary>
            Smear,
            /// <summary>A small curved arc where the mouth met the block.</summary>
            Arc,
            /// <summary>A small warm light on the board around an action.</summary>
            Light,
            /// <summary>The one soft pulse of a boss going down.</summary>
            Pulse,
            /// <summary>What is left on the cell for a breath afterwards.</summary>
            Residue,
            /// <summary>A short smear off the block being dragged into the mouth.</summary>
            Streak,
            /// <summary>Where the tail was, for as long as it takes to see that it moved.</summary>
            Afterimage
        }

        private sealed class Spot
        {
            public Mark Kind;
            public Vector2 At;
            public float Angle;
            public float Size;
            public float Cross;
            public float Clock;
            public float Life;
            public Color Colour;
            public float Strength;
            public Sprite Custom;
            public int Turn;
            public SpriteRenderer Renderer;
        }

        private readonly List<Spot> spots = new List<Spot>();

        /// <summary>A fleck or a mote: four shapes, and never a square.</summary>
        private enum Grain
        {
            Soft,
            Short,
            Round,
            Sliver
        }

        private sealed class Fleck
        {
            public Vector2 From;
            public Vector2 To;
            public float Clock;
            public float Life;
            public float Size;
            public float Spin;
            public Color Colour;
            public Grain Shape;
            public SpriteRenderer Renderer;
        }

        private readonly List<Fleck> flecks = new List<Fleck>();

        private readonly List<SpriteRenderer> shadows = new List<SpriteRenderer>();

        private readonly Stack<SpriteRenderer> spare = new Stack<SpriteRenderer>();

        private Material plainMaterial;

        private MaterialPropertyBlock block;

        private uint seed = 0x51ED2701u;

        // =================================================================== the skin material

        private static bool shaderLooked;

        private static Material skinMaterial;

        /// <summary>
        /// The one material the whole snake is drawn with: the skin shader, or null when it is
        /// missing or unsupported - in which case the snake still draws, just without a surface
        /// response. NEVER instanced per segment; the per-segment values go through a
        /// MaterialPropertyBlock.
        /// </summary>
        public static Material SkinMaterial
        {
            get
            {
                if (!shaderLooked)
                {
                    shaderLooked = true;
                    Shader shader = Shader.Find("ProjectBlock/SnakeSkin");
                    if (shader == null)
                    {
                        shader = Resources.Load<Shader>("Shaders/SnakeSkin");
                    }
                    if (shader != null && shader.isSupported)
                    {
                        skinMaterial = new Material(shader);
                        skinMaterial.hideFlags = HideFlags.HideAndDontSave;
                    }
                }
                return skinMaterial;
            }
        }

        private static readonly int CentreId = Shader.PropertyToID("_Centre");
        private static readonly int HalfId = Shader.PropertyToID("_Half");
        private static readonly int SheenId = Shader.PropertyToID("_Sheen");
        private static readonly int SheenSoftId = Shader.PropertyToID("_SheenSoft");
        private static readonly int LeadId = Shader.PropertyToID("_Lead");
        private static readonly int LeadDirId = Shader.PropertyToID("_LeadDir");
        private static readonly int AccentId = Shader.PropertyToID("_Accent");
        private static readonly int CompressionId = Shader.PropertyToID("_Compression");
        private static readonly int BandId = Shader.PropertyToID("_Band");
        private static readonly int BandWidthId = Shader.PropertyToID("_BandWidth");
        private static readonly int BandStrengthId = Shader.PropertyToID("_BandStrength");
        private static readonly int DrainId = Shader.PropertyToID("_Drain");
        private static readonly int DimId = Shader.PropertyToID("_Dim");
        private static readonly int AccentColourId = Shader.PropertyToID("_AccentColour");
        private static readonly int Pocket0Id = Shader.PropertyToID("_Pocket0");
        private static readonly int PocketAt0Id = Shader.PropertyToID("_PocketAt0");
        private static readonly int Pocket1Id = Shader.PropertyToID("_Pocket1");
        private static readonly int PocketAt1Id = Shader.PropertyToID("_PocketAt1");
        private static readonly int Pocket2Id = Shader.PropertyToID("_Pocket2");
        private static readonly int PocketAt2Id = Shader.PropertyToID("_PocketAt2");
        private static readonly int BandColourId = Shader.PropertyToID("_BandColour");

        // =================================================================== driving it

        /// <summary>Start of a frame: who the board is, and how long the snake is (for the LOD).</summary>
        public void Begin(BoardView owner, int snakeLength)
        {
            view = owner;
            cellSize = owner != null ? owner.CellWorldSize : 0f;
            length = snakeLength;
            shadowsUsed = 0;
        }

        /// <summary>One segment's answer: its surface, and the shadow it sits in.</summary>
        public void Paint(SegmentPose pose)
        {
            if (view == null || cellSize <= 0f)
            {
                return;
            }
            PaintSurface(pose);
            PaintShadow(pose);
            if (pose.Index == 0 && pose.Moving && Layers.ShowSnakeBoardContact)
            {
                Smear(pose);
            }
        }

        /// <summary>End of a frame: nothing more is coming, so a head that stopped stops smearing.</summary>
        public void End(bool moving)
        {
            smearing = moving;
        }

        /// <summary>
        /// One beat of the snake's own animation, answered locally. <paramref name="at"/> is where
        /// it happened, <paramref name="dir"/> the way the snake was pointing, and
        /// <paramref name="look"/> the face of a block when a block is involved - so the crumbs off
        /// an obsidian are obsidian's and the streaks off a gold are gold's.
        /// </summary>
        public void Signal(Hook hook, Vector2 at, Vector2 dir, ClusterBurstView.Look look)
        {
            if (view == null || cellSize <= 0f)
            {
                return;
            }
            float g = Mathf.Max(Style.VfxGlobalIntensity, 0f);
            switch (hook)
            {
                case Hook.WakeSegment:
                    Board(Mark.Oval, at, cellSize * 0.78f, cellSize * 0.5f, 0f,
                        Style.WakeBoardPressure * g, 0.16f);
                    break;
                case Hook.WakeHead:
                    Board(Mark.Oval, at, cellSize * 0.9f, cellSize * 0.58f, 0f,
                        Style.WakeBoardPressure * 1.3f * g, 0.2f);
                    Throw(at, dir, Style.WakeMoteCount, 0.28f, 0.5f, 0.45f,
                        new[] { Grain.Round, Grain.Soft }, new[] { Teal, Cream });
                    break;
                case Hook.MoveStart:
                    smearing = true;
                    lastSmear = at;
                    break;
                case Hook.MoveTurn:
                    // The outer side of the turn lifts (the material does that); the board feels
                    // the weight going round.
                    Board(Mark.Oval, at, cellSize * 0.82f, cellSize * 0.52f, 0f,
                        Style.BoardContactStrength * 0.8f * g, Style.BoardContactDuration);
                    break;
                case Hook.MoveSettle:
                    Board(Mark.Oval, at, cellSize * 0.92f, cellSize * 0.56f, 0f,
                        Style.MoveSettlePressureStrength * g, Style.BoardContactDuration * 0.8f);
                    smearing = false;
                    break;
                case Hook.StuckPressure:
                {
                    Vector2 edge = at + dir * (cellSize * 0.5f);
                    Board(Mark.Ring, edge, cellSize * 0.5f, cellSize * 0.42f,
                        Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg,
                        Style.StuckContactPressure * g, Style.StuckContactDuration);
                    Board(Mark.Oval, edge, cellSize * 0.42f, cellSize * 0.3f, 0f,
                        Style.StuckContactPressure * 0.7f * g, Style.StuckContactDuration * 0.9f);
                    Throw(edge, -dir, Style.StuckDustCount, 0.18f, 0.6f, 0.3f,
                        new[] { Grain.Round }, new[] { Cream, Teal });
                    break;
                }
                case Hook.MouthOpen:
                    // The dark inside the mouth deepens: the swallow has somewhere to go.
                    Board(Mark.Oval, at + dir * (cellSize * 0.22f), cellSize * 0.44f,
                        cellSize * 0.34f, 0f, Style.BiteMouthShadowStrength * 0.6f * g, 0.14f);
                    break;
                case Hook.BiteContact:
                {
                    float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                    Board(Mark.Arc, at + dir * (cellSize * 0.14f), cellSize * 0.66f,
                        cellSize * 0.46f, angle, Style.BiteContactArcStrength * g,
                        Style.BiteContactArcDuration);
                    Board(Mark.Arc, at + dir * (cellSize * 0.26f), cellSize * 0.44f,
                        cellSize * 0.3f, angle, Style.BiteContactArcStrength * 0.7f * g,
                        Style.BiteContactArcDuration * 0.8f);
                    Board(Mark.Light, at + new Vector2(dir.x * 0.05f,
                            -Style.LocalLightGroundDrop) * cellSize,
                        Style.LocalLightRadius * cellSize,
                        Style.LocalLightRadius * Style.LocalLightGroundFlatten * cellSize, 0f,
                        Style.LocalLightStrength * g, 0.16f);
                    Throw(at, dir, Style.BiteFleckCount, Style.BiteFleckSpeed * 0.3f,
                        Style.BiteFleckSpeed, Style.BiteFleckLifetime,
                        new[] { Grain.Short, Grain.Soft }, new[] { Cream, Amber });
                    break;
                }
                // THE BLOCK GOING IN IS NOT DRAWN HERE ANY MORE. This used to lay three
                // straight tapered smears across the jaw in the block's tint, which on a painted
                // tile is WHITE and on a protected one MAGENTA - the pink lines in the
                // screenshots, and the debug-laser look the whole rebuild exists to remove. The
                // block's matter now leaves along SnakeEatView's filaments, which are curved,
                // volumetric, and the colour of the block itself.
                case Hook.GulpPass:
                    Throw(at, dir, 1, 0.12f, 0.35f, 0.26f, new[] { Grain.Round },
                        new[] { Cream, Amber });
                    break;
                case Hook.GrowthSettle:
                    Board(Mark.Oval, at, cellSize * 0.88f, cellSize * 0.54f, 0f,
                        Style.GrowthContactPressure * g, 0.08f);
                    break;
                case Hook.LineSnakeImpact:
                    Throw(at, dir, Style.CutImpactFleckCount, 0.16f, 0.5f, 0.22f,
                        new[] { Grain.Round, Grain.Soft }, new[] { Teal, Cream });
                    break;
                case Hook.CutSignalPass:
                    break; // the body IS the signal - see the band in the skin shader
                case Hook.TailPinch:
                    Board(Mark.Ring, at, cellSize * 0.46f, cellSize * 0.4f, 0f,
                        Style.TailPinchShadowStrength * g, 0.1f);
                    break;
                case Hook.TailDetach:
                {
                    // Not a radial burst: a couple at the root, a couple with the recoil, one that
                    // just drifts.
                    int n = Mathf.Max(Style.TailDetachFleckCount, 3);
                    Vector2 root = at - dir * (cellSize * 0.35f);
                    Throw(root, -dir, n / 2, 0.1f, 0.35f, Style.DefeatMoteLifetime * 0.6f,
                        new[] { Grain.Soft }, new[] { Teal, Cream });
                    Throw(at, dir, n - n / 2 - 1, Style.TailDetachFleckDistance * 0.6f,
                        Style.TailDetachFleckDistance, 0.3f, new[] { Grain.Short, Grain.Sliver },
                        new[] { Cream, Amber });
                    Throw(at, new Vector2(0f, 1f), 1, 0.1f, 0.22f, 0.42f, new[] { Grain.Round },
                        new[] { Teal });
                    break;
                }
                case Hook.NewTailSettled:
                    Board(Mark.Oval, at, cellSize * 0.7f, cellSize * 0.44f, 0f,
                        Style.NewTailContactPressure * g, 0.09f);
                    break;
                case Hook.DefeatStart:
                    break; // a last surge of sheen, which the material carries
                case Hook.DefeatCollapse:
                    Board(Mark.Residue, at, cellSize * 0.8f, cellSize * 0.8f, 0f,
                        Style.DefeatResidueStrength * g, Style.DefeatResidueDuration);
                    break;
                case Hook.DefeatMote:
                    // Three families, three shapes, and hardly any distance.
                    Throw(at, new Vector2(0f, 1f), Style.DefeatMoteCount / 3,
                        Style.DefeatMoteDistance * 0.5f, Style.DefeatMoteDistance,
                        Style.DefeatMoteLifetime, new[] { Grain.Round }, new[] { Teal });
                    Throw(at, new Vector2(0.7f, 0.7f), Style.DefeatMoteCount / 3,
                        Style.DefeatMoteDistance * 0.4f, Style.DefeatMoteDistance * 0.9f,
                        Style.DefeatMoteLifetime, new[] { Grain.Soft }, new[] { Cream });
                    Throw(at, new Vector2(-0.7f, 0.6f), Style.DefeatMoteCount - 2 * (Style.DefeatMoteCount / 3),
                        Style.DefeatMoteDistance * 0.5f, Style.DefeatMoteDistance,
                        Style.DefeatMoteLifetime * 1.2f, new[] { Grain.Sliver }, new[] { Amber });
                    break;
                case Hook.DefeatComplete:
                    Board(Mark.Pulse, at, Style.DefeatBoardPulseRadius * 2f * cellSize,
                        Style.DefeatBoardPulseRadius * 2f * cellSize, 0f,
                        Style.DefeatBoardPulseStrength * g, Style.DefeatBoardPulseDuration);
                    break;
            }
        }

        /// <summary>Everything transient goes at once - a new arena, the lab closing, a run ending.</summary>
        public void Stop()
        {
            for (int i = 0; i < spots.Count; i++)
            {
                Return(spots[i].Renderer);
            }
            spots.Clear();
            for (int i = 0; i < flecks.Count; i++)
            {
                Return(flecks[i].Renderer);
            }
            flecks.Clear();
            for (int i = 0; i < shadows.Count; i++)
            {
                Return(shadows[i]);
            }
            shadows.Clear();
            shadowsUsed = 0;
            smearing = false;
        }

        // =================================================================== the surface

        /// <summary>The skin's answer to what this segment is doing. One material, one property
        /// block: nothing here instances anything per segment.</summary>
        private void PaintSurface(SegmentPose pose)
        {
            Material skin = SkinMaterial;
            if (skin == null || pose.Renderer == null)
            {
                return;
            }
            if (!Layers.ShowSnakeMaterialFx)
            {
                pose.Renderer.SetPropertyBlock(null);
                return;
            }
            if (block == null)
            {
                block = new MaterialPropertyBlock();
            }
            block.Clear();
            float g = Mathf.Max(Style.VfxGlobalIntensity, 0f);
            Vector3 centre = transform.TransformPoint(new Vector3(pose.At.x, pose.At.y, 0f));
            float scale = transform.lossyScale.x;
            block.SetVector(CentreId, new Vector4(centre.x, centre.y, 0f, 0f));
            block.SetFloat(HalfId, Mathf.Max(Mathf.Max(pose.Width, pose.Height) * 0.5f
                * (scale > 0f ? scale : 1f), 1e-4f));
            block.SetFloat(SheenId, Mathf.Clamp01((Style.SurfaceSheenStrength + pose.Sheen) * g));
            block.SetFloat(SheenSoftId, Style.SurfaceSheenSoftness);
            block.SetFloat(LeadId, Mathf.Clamp01(pose.Lead * Style.LeadingEdgeHighlightStrength
                * (pose.Moving ? Style.MoveLeadingHighlight * 2f : 1f) * g));
            block.SetVector(LeadDirId, new Vector4(pose.Facing.x, pose.Facing.y, 0f, 0f));
            block.SetFloat(AccentId, Mathf.Clamp01(pose.Accent * g));
            block.SetFloat(CompressionId, Mathf.Clamp01(pose.Compression));
            block.SetFloat(BandId, pose.Band);
            block.SetFloat(BandWidthId, 0.38f);
            block.SetFloat(BandStrengthId, Mathf.Clamp01(pose.BandStrength * g));
            block.SetFloat(DrainId, Mathf.Clamp01(pose.Drain));
            block.SetFloat(DimId, Mathf.Clamp01(pose.Dim));
            block.SetColor(AccentColourId,
                pose.AccentColour.a > 0f ? pose.AccentColour : Amber);
            // What it swallowed, coming up from underneath. Zero strength costs nothing.
            block.SetColor(Pocket0Id, pose.Pocket0);
            block.SetVector(PocketAt0Id, pose.PocketAt0);
            block.SetColor(Pocket1Id, pose.Pocket1);
            block.SetVector(PocketAt1Id, pose.PocketAt1);
            block.SetColor(Pocket2Id, pose.Pocket2);
            block.SetVector(PocketAt2Id, pose.PocketAt2);
            block.SetColor(BandColourId, Cream);
            pose.Renderer.SetPropertyBlock(block);
        }

        // =================================================================== the shadow

        /// <summary>
        /// The shadow a segment sits in: stretched along the body so it is not the same ellipse
        /// stamped twenty times, smaller under the tail, wider and fainter under a dormant one,
        /// tight at rest and softer while it moves.
        /// </summary>
        private void PaintShadow(SegmentPose pose)
        {
            if (!Layers.ShowSnakeShadows)
            {
                return;
            }
            while (shadows.Count <= shadowsUsed)
            {
                shadows.Add(Rent(ShadowSprite(), ShadowOrder, null));
            }
            SpriteRenderer r = shadows[shadowsUsed++];
            bool horizontal = Mathf.Abs(pose.Facing.x) > 0.5f;
            float along = pose.Piece == Part.Tail ? 0.74f : pose.Piece == Part.Head ? 0.92f : 1f;
            float across = pose.Piece == Part.Tail ? 0.6f : pose.Piece == Part.Swollen ? 0.86f : 0.72f;
            if (pose.Piece == Part.Bend)
            {
                // A corner's footprint is closer to square than a straight's.
                along = 0.88f;
                across = 0.88f;
            }
            float soften = pose.Moving ? Style.ShadowMoveSoftening : 0f;
            // Its own softness: a softer shadow spreads and thins, a tight one bites.
            float soft = Mathf.Lerp(0.92f, 1.14f, Mathf.Clamp01(Style.ShadowSoftness));
            float w = pose.Width * (horizontal ? along : across) * soft * (1f + 0.08f * soften);
            float h = pose.Height * (horizontal ? across : along) * soft * (1f + 0.08f * soften);
            float opacity = Style.ShadowOpacity * Mathf.Lerp(1.1f, 0.85f, Style.ShadowSoftness)
                * Mathf.Max(Style.VfxGlobalIntensity, 0f)
                * pose.Alpha * (pose.ShadowFade > 0f ? pose.ShadowFade : 1f)
                * (1f - 0.4f * soften)
                * Mathf.Lerp(1f, 1f - Style.WakeShadowTightening, pose.Drain);
            // A dormant segment's shadow is wider and fainter, and tightens as it wakes.
            float spread = 1f + Style.WakeShadowTightening * 0.35f * pose.Drain;
            PlaceRect(r, pose.At + new Vector2(0f, -0.035f * cellSize), w * spread, h * spread,
                ShadowColour, opacity, 0f);
        }

        /// <summary>The mark the body's weight leaves as it slides. Dropped every third of a cell,
        /// and gone in a tenth of a second: it is a contact, not a trail.</summary>
        private void Smear(SegmentPose pose)
        {
            if (!smearing || Style.MoveContactSmearStrength <= 0f)
            {
                return;
            }
            if ((pose.At - lastSmear).sqrMagnitude < cellSize * cellSize * 0.11f)
            {
                return;
            }
            lastSmear = pose.At;
            float lod = length >= Style.LodLongLength ? Style.LodLongContact : 1f;
            Board(Mark.Smear, pose.At, cellSize * 0.62f, cellSize * 0.34f,
                Mathf.Atan2(pose.Facing.y, pose.Facing.x) * Mathf.Rad2Deg,
                Style.MoveContactSmearStrength * lod * Mathf.Max(Style.VfxGlobalIntensity, 0f),
                Style.MoveContactSmearDuration);
        }

        // =================================================================== transients

        private Spot Board(Mark kind, Vector2 at, float size, float cross, float angle,
            float strength, float life)
        {
            if (strength <= 0.002f || life <= 0f)
            {
                return null;
            }
            if (!Layers.ShowSnakeBoardContact && kind != Mark.Afterimage)
            {
                return null;
            }
            var spot = new Spot
            {
                Kind = kind,
                At = at,
                Size = size,
                Cross = cross,
                Angle = angle,
                Strength = strength,
                Life = life,
                Colour = kind == Mark.Light || kind == Mark.Pulse || kind == Mark.Residue
                    ? Amber : kind == Mark.Arc || kind == Mark.Streak ? Cream : ContactColour
            };
            spot.Renderer = Rent(SpriteFor(kind), kind == Mark.Light || kind == Mark.Pulse
                ? LightOrder
                : kind == Mark.Arc || kind == Mark.Streak ? ContactOrder : BoardOrder, null);
            spots.Add(spot);
            return spot;
        }

        /// <summary>Where the tail was, for a breath after it let go.</summary>
        public void Afterimage(Sprite sprite, Vector2 at, float size, int turn)
        {
            if (sprite == null || Style.TailDetachAfterimageStrength <= 0f
                || !Layers.ShowSnakeMaterialFx)
            {
                return;
            }
            var spot = new Spot
            {
                Kind = Mark.Afterimage,
                At = at,
                Size = size,
                Cross = size,
                Turn = turn,
                Custom = sprite,
                Strength = Style.TailDetachAfterimageStrength
                    * Mathf.Max(Style.VfxGlobalIntensity, 0f),
                Life = 0.06f,
                Colour = Teal
            };
            spot.Renderer = Rent(sprite, BoardOrder, null);
            spots.Add(spot);
        }

        /// <summary>A few flecks or motes, in the shapes and colours given, deterministic and
        /// capped: the budget never grows with the snake.</summary>
        private void Throw(Vector2 at, Vector2 dir, int count, float near, float far, float life,
            Grain[] shapes, Color[] colours)
        {
            if (!Layers.ShowSnakeParticles || count <= 0)
            {
                return;
            }
            int lod = length >= Style.LodLongLength
                ? Mathf.Max(1, Mathf.RoundToInt(count * Style.LodLongContact)) : count;
            int room = Mathf.Max(0, Style.FleckBudget - flecks.Count);
            int n = Mathf.Min(lod, room);
            var dice = new Dice(Next());
            for (int i = 0; i < n; i++)
            {
                float spread = dice.Range(-0.7f, 0.7f);
                Vector2 across = new Vector2(-dir.y, dir.x) * spread;
                Vector2 away = (dir + across).normalized;
                var f = new Fleck
                {
                    From = at + away * (near * cellSize * 0.35f),
                    To = at + away * (dice.Range(near, far) * cellSize),
                    Life = life * dice.Range(0.85f, 1.15f),
                    Size = cellSize * dice.Range(0.05f, 0.085f),
                    Spin = dice.Range(-25f, 25f),
                    Shape = shapes[(int)(dice.Next() * shapes.Length) % shapes.Length],
                    Colour = colours[(int)(dice.Next() * colours.Length) % colours.Length]
                };
                f.Renderer = Rent(SpriteFor(f.Shape), FleckOrder, null);
                flecks.Add(f);
            }
        }

        private void Update()
        {
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            for (int i = spots.Count - 1; i >= 0; i--)
            {
                Spot s = spots[i];
                s.Clock += dt;
                float k = Mathf.Clamp01(s.Clock / Mathf.Max(s.Life, 0.001f));
                float fade = s.Kind == Mark.Pulse || s.Kind == Mark.Light
                    ? 1f - Smooth(k)
                    : Mathf.Sin(Mathf.PI * Mathf.Clamp01(k * 1.15f));
                float grow = s.Kind == Mark.Pulse ? Mathf.Lerp(0.45f, 1f, Smooth(k)) : 1f;
                PlaceRect(s.Renderer, s.At, s.Size * grow, s.Cross * grow, s.Colour,
                    s.Strength * fade, s.Angle + 90f * s.Turn);
                if (k >= 1f)
                {
                    Return(s.Renderer);
                    spots.RemoveAt(i);
                }
            }
            for (int i = flecks.Count - 1; i >= 0; i--)
            {
                Fleck f = flecks[i];
                f.Clock += dt;
                float k = Mathf.Clamp01(f.Clock / Mathf.Max(f.Life, 0.001f));
                // Out and slowing, with a touch of lift - never a spray.
                Vector2 at = Vector2.Lerp(f.From, f.To, Smooth(k));
                at.y += 0.06f * cellSize * Smooth(k) * (1f - k);
                float size = f.Size * (1f - 0.45f * k);
                PlaceRect(f.Renderer, at, size * (f.Shape == Grain.Sliver ? 2.2f : 1f), size,
                    f.Colour, (1f - Smooth(k)) * 0.9f, f.Spin * k);
                if (k >= 1f)
                {
                    Return(f.Renderer);
                    flecks.RemoveAt(i);
                }
            }
        }

        // =================================================================== renderers

        private static void PlaceRect(SpriteRenderer r, Vector2 at, float width, float height,
            Color colour, float alpha, float angle)
        {
            if (r == null)
            {
                return;
            }
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
            if (spare.Count > 0)
            {
                r = spare.Pop();
            }
            else
            {
                var go = new GameObject("SnakeVfx");
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
            r.color = Clear;
            spare.Push(r);
        }

        // =================================================================== art
        //
        // None of this is the snake: it is the shadow it sits in, the mark it leaves and the few
        // flecks an action throws. Four grain shapes, so nothing ever looks like square confetti.

        private static readonly Dictionary<int, Sprite> sprites = new Dictionary<int, Sprite>();

        private static Sprite SpriteFor(Mark kind)
        {
            switch (kind)
            {
                case Mark.Ring: return Cached(1, () => Ring(64, 0.42f, 0.07f));
                case Mark.Arc: return Cached(2, () => Arc(64));
                case Mark.Light: return Cached(3, () => Blob(64, 0.5f, 0.5f));
                case Mark.Pulse: return Cached(4, () => Rounded(64, 0.2f, 0.3f));
                case Mark.Residue: return Cached(5, () => Blob(64, 0.42f, 0.36f));
                case Mark.Streak: return Cached(6, () => Streak(64));
                default: return ShadowSprite();
            }
        }

        private static Sprite SpriteFor(Grain grain)
        {
            switch (grain)
            {
                case Grain.Short: return Cached(11, () => Streak(32));
                case Grain.Round: return Cached(12, () => Blob(16, 0.46f, 0.2f));
                case Grain.Sliver: return Cached(13, () => Streak(24));
                default: return Cached(10, () => Blob(24, 0.4f, 0.32f));
            }
        }

        private static Sprite ShadowSprite()
        {
            return Cached(0, () => Blob(64, 0.34f, 0.3f));
        }

        /// <summary>The soft round blot this layer already generates, shared with the bite
        /// (SnakeEatView) so its motes and lights cost no texture of their own.</summary>
        public static Sprite SoftDot()
        {
            return Cached(3, () => Blob(64, 0.5f, 0.5f));
        }

        private static Sprite Cached(int key, System.Func<Sprite> make)
        {
            Sprite sprite;
            if (!sprites.TryGetValue(key, out sprite) || sprite == null)
            {
                sprite = make();
                sprites[key] = sprite;
            }
            return sprite;
        }

        /// <summary>A soft round blot: the shadow, the lights, the round motes.</summary>
        private static Sprite Blob(int n, float radius, float softness)
        {
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float u = (x + 0.5f) / n - 0.5f;
                    float v = (y + 0.5f) / n - 0.5f;
                    float d = Mathf.Sqrt(u * u + v * v);
                    float a = Mathf.Clamp01((radius - d) / Mathf.Max(softness, 0.001f));
                    px[y * n + x] = new Color32(255, 255, 255,
                        (byte)Mathf.RoundToInt(a * a * 255f));
                }
            }
            return Make(n, n, px);
        }

        /// <summary>A soft ring: the mark of a shove, and the pinch at a tail root.</summary>
        private static Sprite Ring(int n, float radius, float width)
        {
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float u = (x + 0.5f) / n - 0.5f;
                    float v = (y + 0.5f) / n - 0.5f;
                    float d = Mathf.Sqrt(u * u + v * v);
                    float a = Mathf.Clamp01(1f - Mathf.Abs(d - radius) / Mathf.Max(width, 0.001f));
                    px[y * n + x] = new Color32(255, 255, 255,
                        (byte)Mathf.RoundToInt(a * a * 255f));
                }
            }
            return Make(n, n, px);
        }

        /// <summary>A thin curved arc, open along +x: what a mouth leaves where it closed.</summary>
        private static Sprite Arc(int n)
        {
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float u = (x + 0.5f) / n - 0.5f;
                    float v = (y + 0.5f) / n - 0.5f;
                    float d = Mathf.Sqrt(u * u + v * v);
                    float ring = Mathf.Clamp01(1f - Mathf.Abs(d - 0.36f) / 0.115f);
                    // Only the leading third of the circle, and softly.
                    float side = Mathf.Clamp01((u / Mathf.Max(d, 1e-4f) - 0.25f) / 0.75f);
                    px[y * n + x] = new Color32(255, 255, 255,
                        (byte)Mathf.RoundToInt(ring * side * side * 255f));
                }
            }
            return Make(n, n, px);
        }

        /// <summary>A short tapered smear along +x: the ingestion streaks, the short flecks.</summary>
        private static Sprite Streak(int n)
        {
            int h = Mathf.Max(4, n / 4);
            var px = new Color32[n * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float u = (x + 0.5f) / n;
                    float v = Mathf.Abs((y + 0.5f) / h - 0.5f) * 2f;
                    float taper = Mathf.Clamp01(1f - u) * Mathf.Clamp01(u / 0.25f);
                    float a = Mathf.Clamp01(1f - v / Mathf.Max(taper, 0.001f)) * taper;
                    px[y * n + x] = new Color32(255, 255, 255,
                        (byte)Mathf.RoundToInt(Mathf.Clamp01(a) * 255f));
                }
            }
            return Make(n, h, px);
        }

        /// <summary>A rounded-square diffusion: the board's own shape, for the defeat's pulse.</summary>
        private static Sprite Rounded(int n, float radius, float softness)
        {
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float u = Mathf.Abs((x + 0.5f) / n - 0.5f);
                    float v = Mathf.Abs((y + 0.5f) / n - 0.5f);
                    float dx = Mathf.Max(u - (0.5f - radius), 0f);
                    float dy = Mathf.Max(v - (0.5f - radius), 0f);
                    float d = Mathf.Sqrt(dx * dx + dy * dy) + Mathf.Max(u, v) * 0.35f;
                    float a = Mathf.Clamp01((0.42f - d) / Mathf.Max(softness, 0.001f));
                    px[y * n + x] = new Color32(255, 255, 255,
                        (byte)Mathf.RoundToInt(a * a * 255f));
                }
            }
            return Make(n, n, px);
        }

        private static Sprite Make(int w, int h, Color32[] px)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.hideFlags = HideFlags.HideAndDontSave;
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.SetPixels32(px);
            tex.Apply(false, false);
            Sprite sprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), w);
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        // =================================================================== maths

        private static float Smooth(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * (3f - 2f * x);
        }

        /// <summary>The next seed. Deterministic: the same action always throws the same flecks.</summary>
        private uint Next()
        {
            unchecked
            {
                seed ^= seed << 13;
                seed ^= seed >> 17;
                seed ^= seed << 5;
                return seed;
            }
        }

        private struct Dice
        {
            private uint state;

            public Dice(uint value)
            {
                state = value == 0u ? 0x9E3779B9u : value;
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
    }
}
