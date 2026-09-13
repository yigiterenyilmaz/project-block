// PURPOSE: "Parazit"'s HOST CUBE - SYMBIOTIC CLASP. The cube a joker is riding, drawn as a separate
// organism gripping it rather than as a colour applied to it. Owned by BoardView like the rot, the
// snake and the press, because a host stands for as long as its block does and the board is
// repainted many times in that.
//
// WHAT IT REPLACES. The host cube used to be its own colour lerped 55% toward magenta. That said
// "this one is pink" and nothing else: not that something is holding it down, not that a joker is
// riding it, not why a power bounced off it, and not what a line clear is about to cost. THE CUBE
// UNDERNEATH NOW KEEPS EVERYTHING - its sprite, its colour, its element, its material - and the
// parasite is geometry on top of it:
//
//   MEMBRANE      the main layer, and the reason this reads as a parasite at all: a thin
//                 TRANSLUCENT living film wrapping the cube's face. Its contour is irregular - it
//                 runs near the cube's edge in places and pulls back toward the middle in others -
//                 it is thicker in some regions than others, and where it is THIN the block's own
//                 colour shows through. It spills a couple of pixels over the bevel, so it wraps
//                 rather than sits. Drawn on a subdivided mesh (ParasiteMembraneMesh) because of
//                 the idle below.
//   WRAP FOLDS    two or three THICK GATHERS of the film itself, crossing the face on broad
//                 curves. A wrap lying perfectly flat on a cube is a decal; anything real pulled
//                 over a solid thing heaps up, and the heaps are what say there is MATERIAL here
//                 rather than a tint. They live IN the membrane's shader (_FoldA/B/C) as shading
//                 and thickness, not as pieces over it, because a gather has no silhouette of its
//                 own - it is the sheet. Drawn as geometry they were a chain of convex segments,
//                 which is a tube however wide it is made, and a tube crossing a cube is a cable.
//   NEGATIVE      two HOLES in the film, where the block is simply itself - no membrane and no
//   SPACE         drain, seen THROUGH the wrap rather than under it, with the film gathered into a
//                 thicker lit lip around each. A sheet with no holes in it reads as a filter over
//                 the cell; the holes are what make the cube and the thing on it two objects.
//   WRAP RIBS     two to four thinner curved BINDING STRANDS inside the film, on asymmetric paths
//                 that cross the face rather than running corner-to-centre. Tension bands: they are
//                 what is actually holding the cube down. Never a perfect X and never four of
//                 anything in the corners.
//   NEST CORE     one small lobed body, slightly OFF centre, where the strands gather.
//   ESSENCE       inside it, the bound joker's OWN accent colour in a small seed. Losing the cube
//                 means losing that joker, so a player who cannot see which one has not been told
//                 what the line clear will cost. The colour comes from the passenger's def id,
//                 never from the host cube's material, which says nothing about who is riding.
//
// TWO EARLIER PASSES ARE BURIED HERE, and both were the same mistake at different scales: a colour
// applied to the cube (a 55% magenta wash - "this one is pink"), and then a set of parts bolted to
// it (four rounded squares, four strips, a centre dot - "a UI lock"). Neither said the thing the
// mechanic is about. The cube is not marked and it is not clamped: it is WRAPPED, and the wrap is
// alive.
//
// The cube reads as a cube first and a host second: roughly four fifths of what is on the cell is
// still the block itself.
//
// FOUR EVENTS, and the View decides none of them (see ParasiteVisuals - Core reports every refusal
// with the direction it came from, when it had one):
//   SEATING    the block lands, then the parasite LOCKS ON: anchors unfold into the corners and
//              clamp, tethers grow to the middle, the node forms, the passenger wakes, everything
//              latches. It never simply pops in.
//   IDLE       THE CUBE TRIES TO GET OUT, AND THE PARASITE PUTS IT BACK. Every few seconds the
//              film BULGES in one region - the cube pushing from underneath - the film thins there
//              so the block's own colour comes through, the cube loads half a pixel that way, the
//              strands nearest it tension and straighten, the core is pulled the OTHER way holding
//              on, the passenger brightens for a moment, and then the wrap contracts and presses
//              the cube back to its cell. No particles, no constant glow, no wobble: the cube is
//              still almost all of the time, and what moves is the thing on top of it.
//   CLAMP      something tried to destroy or move it and the rules said no. The parasite does not
//              raise a shield: it GRIPS HARDER, on the side the force came from first, the far
//              anchors counter-bracing behind it, the tethers tensioning, the node compressing and
//              the shadow locking. The cube loads a pixel into the force and does not leave its cell.
//   SEVERANCE  the player's own line reaches it, and this is the one thing the parasite cannot
//              hold. The bonds do not vanish - they TENSION, THIN AND BREAK one at a time, each
//              half retracting into the anchor and the node it came from; the anchors lose their
//              grip and lift off; the last tethers overstretch; the node's shell opens and the
//              passenger is fully visible for a beat; the cube goes through the line's own
//              destruction; and then the passenger collapses inward on its own colour and is gone.
//              The player must be able to read: I lost the cube AND the joker on it.

using System.Collections.Generic;
using ProjectBlock.Core;
using UnityEngine;

namespace ProjectBlock.View
{
    /// <summary>The parasite's clasp on a host cube: its anchors, tethers, node and passenger.</summary>
    public sealed class ParasiteHostView : MonoBehaviour
    {
        // =================================================================== TUNING
        /// <summary>Everything that decides how a host cube READS. Sizes as a share of a CUBE,
        /// times in seconds.</summary>
        public static class Style
        {
            // ---- the wrap at rest ----
            /// <summary>How opaque the film is where it is at its normal thickness. Low: the block
            /// underneath has to stay the first thing the eye reads.</summary>
            public static float MembraneOpacity = 0.62f;

            /// <summary>How much its thickness varies across the face.</summary>
            public static float MembraneThickness = 0.55f;

            /// <summary>How far its contour reaches toward the cube's edge, how strongly the lobes
            /// push it in and out, and how soft the boundary is. The lobes are what stop it being
            /// the square it is drawn on.</summary>
            public static float MembraneReach = 0.88f;

            public static float MembraneLobes = 0.13f;

            public static float MembraneEdgeSoftness = 0.09f;

            /// <summary>How far the film spills over the cube's bevel - what makes it WRAP rather
            /// than sit on the face.</summary>
            public static float MembraneEdgeWrap = 0.6f;

            /// <summary>How much bigger than the cube the film is drawn, so the wrap has somewhere
            /// to go over the edge.</summary>
            public static float MembraneOversize = 1.1f;

            /// <summary>Satin, and LOW. A bright sheen is what made the first film read as
            /// glass.</summary>
            public static float MembraneSheen = 0.14f;

            // ---- what the wrap is taking out of the block ----
            /// <summary>How much life the film pulls out of the cube where it lies thick. This is
            /// the withering, and without it a parasite is just an overlay.</summary>
            public static float DrainStrength = 0.9f;

            public static float DrainSaturationLoss = 0.78f;

            public static float DrainBrightnessLoss = 0.34f;

            /// <summary>How far the block's own HIGHLIGHT is put out. A cube's life is in the
            /// bright band its bevel catches; leave that and the drain is only a colour filter.
            /// </summary>
            public static float DrainHighlightKill = 0.7f;

            /// <summary>How sharply the drain follows coverage. Above 1 keeps the thin edges honest
            /// while making the heavy middle genuinely dead.</summary>
            public static float DrainCoverageCurve = 1.4f;

            /// <summary>What the drained colour leans toward - a dead mauve, never grey and never
            /// black.</summary>
            public static readonly Color DrainDead = new Color(0.28f, 0.2f, 0.28f);

            /// <summary>The RECAPTURE PAYMENT. When the wrap has pushed the cube back down it
            /// takes its price at once: a short surge of the drain over the WHOLE face, not only
            /// the region that struggled. That surge is the difference between an idle that is a
            /// fidget and an idle that is a losing fight - the cube tried, and it is worse off for
            /// having tried.</summary>
            public static float WitherPulseStrength = 0.45f;

            public static float WitherPulseDuration = 0.34f;

            /// <summary>What each struggle leaves behind PERMANENTLY (up to a cap): the region
            /// stays a little deader than it was, and a host that has fought several times is
            /// visibly further gone than one that has just been seated.</summary>
            public static float AfterDrainGain = 0.12f;

            public static float AfterDrainMax = 0.34f;

            /// <summary>How fast that residue fades, per second. Slow - it is a scar, not a
            /// flash.</summary>
            public static float AfterDrainDecay = 0.02f;

            /// <summary>THE COLOUR COMES BACK AS THE FILM GIVES. The instant the tear opens the
            /// drain is off and the block is its own colour again for a beat - and then the line
            /// takes it anyway. Short and SNAPPED, never a fade: a slow return reads as the effect
            /// ending, and this has to read as the block getting free a moment too late.</summary>
            public static float DeathColourReturn = 0.055f;

            // ---- the dead thickenings ----
            /// <summary>How many necrotic patches a host gets, and how big. These are the dark,
            /// matte, almost unlit regions that stop the wrap reading as clean film.</summary>
            public static int PatchCountMin = 2;

            public static int PatchCountMax = 4;

            public static float PatchSizeMin = 0.26f;

            public static float PatchSizeMax = 0.46f;

            public static float PatchOpacity = 0.82f;

            // ---- veins: SECONDARY detail, never the main read ----
            public static int VeinCount = 5;

            public static int VeinSegments = 7;

            public static float VeinWidth = 0.022f;

            public static float VeinOpacity = 0.5f;

            public static float VeinCurve = 0.16f;

            // ---- the thick gathers of the film ----
            /// <summary>Two or three, and no more: the gathers are the heaviest thing on the face
            /// after the film itself, and four of them close the cube up.</summary>
            public static int FoldCount = 3;

            /// <summary>How wide one gather is, as a share of the cube - measured from its crest
            /// to where it meets the flat film, so the whole ridge is twice this. It is a broad
            /// HEAP, not a cord: much narrower and it is a cable again.</summary>
            public static float FoldWidth = 0.19f;

            /// <summary>How far a gather bows off a straight line across the face.</summary>
            public static float FoldCurve = 0.22f;

            /// <summary>How strongly a gather is lit and creased. It only ever lerps the film's own
            /// colour brighter or deeper - a gather is never a different colour laid over it.
            /// </summary>
            public static float FoldRelief = 1f;

            /// <summary>How far the wrap tightening FLATTENS the gathers. A sheet pulled taut has
            /// fewer folds in it; that is most of what makes a clamp read as a clamp.</summary>
            public static float FoldTensionFlatten = 0.55f;

            // ---- the holes in the film ----
            /// <summary>How big a window is, in UV - so 0.11 is a bit under a quarter of the face
            /// across.</summary>
            public static float WindowRadiusMin = 0.085f;

            public static float WindowRadiusMax = 0.155f;

            /// <summary>How much smaller the second hole is than the first.</summary>
            public static float WindowSecondScale = 0.52f;

            /// <summary>How completely the film is gone inside one. Never quite 1: a hole with a
            /// hard edge is a punched circle.</summary>
            public static float WindowStrength = 0.92f;

            /// <summary>Two to four binding strands. Not one per corner - they cross the face.
            /// </summary>
            public static int RibCount = 3;

            public static int RibSegments = 9;

            /// <summary>A strand's width at its ends and through its middle, as a share of a cube.
            /// </summary>
            public static float RibEndWidth = 0.05f;

            public static float RibMidWidth = 0.085f;

            public static float RibCurve = 0.2f;

            public static float RibOpacity = 0.95f;

            /// <summary>The nest core, as a share of the cube, and how far off centre it sits.
            /// </summary>
            public static float CoreSize = 0.275f;

            /// <summary>How strongly the nest's growth ridges are drawn. It is a SHELL that has
            /// grown in stages, not a bead with a highlight on it.</summary>
            public static float CoreRidges = 0.85f;

            /// <summary>Its own contour light - what keeps a small dark body off a dark block.
            /// </summary>
            public static float CoreRimBoost = 0.25f;

            public static float CoreOffset = 0.05f;

            public static float CoreShadowStrength = 0.3f;

            /// <summary>THE PASSENGER IS LAYERED: a dark outer husk, the joker's own colour in
            /// the middle of it and a pale centre inside that. One flat dot of a hue is a status
            /// light; three shells of the same hue is something alive being carried.</summary>
            public static float EssenceSize = 0.115f;

            public static float EssenceOuterScale = 1.55f;

            public static float EssenceInnerScale = 0.44f;

            /// <summary>How dark the husk goes and how pale the centre comes up - both off the
            /// passenger's OWN colour, so the identity survives the layering.</summary>
            public static float EssenceOuterDarken = 0.42f;

            public static float EssenceInnerLighten = 0.55f;

            public static float EssenceBrightness = 1f;

            /// <summary>PARASITE ENVELOPMENT, in the order it happens: the core wakes out of the
            /// surface, the film spreads from it in lobes, the strands rise inside the film, the
            /// film reaches the edges and wraps down, the passenger ignites, and one restraint
            /// pulse locks the whole thing on.</summary>
            public static float SeatCoreWake = 0.11f;

            public static float SeatCoreStartScale = 0.5f;

            public static float SeatMembraneSpread = 0.18f;

            public static float SeatRibForm = 0.16f;

            public static float SeatRibStagger = 0.03f;

            public static float SeatEdgeGrip = 0.15f;

            public static float SeatEssence = 0.12f;

            public static float SeatFinalClamp = 0.13f;

            public static float SeatTotal = 0.62f;

            // ---- idle: the cube tries to get out ----
            public static float IdleMinInterval = 2.6f;

            public static float IdleMaxInterval = 4.4f;

            /// <summary>One escape attempt, end to end.</summary>
            public static float IdleDuration = 0.4f;

            /// <summary>How far the film lifts where the cube pushes, and how wide that region is
            /// (in UV, so 0.3 is under a third of the face).</summary>
            public static float IdleBulgeStrength = 0.55f;

            public static float IdleBulgeRadius = 0.34f;

            /// <summary>How far the cube itself loads that way. Half a pixel - it never leaves.
            /// </summary>
            public static float IdleCubeLoad = 0.012f;

            /// <summary>How hard the strands answer, and how far the core is pulled the other way
            /// holding on.</summary>
            public static float IdleRibTension = 0.7f;

            public static float IdleCoreCounter = 0.018f;

            public static float IdleEssenceBoost = 0.12f;

            /// <summary>After the wrap has pushed the cube back, the region it struggled in is left
            /// a little more drained for a moment - the struggle COST it something. That beat is
            /// most of why the idle reads as a losing fight rather than a fidget.</summary>
            public static float AfterDrainStrength = 0.4f;

            public static float AfterDrainDuration = 0.16f;

            /// <summary>The rarer second idle: a quiet circulation out of the core.</summary>
            public static float CirculationMinInterval = 5f;

            public static float CirculationMaxInterval = 8f;

            public static float CirculationDuration = 0.5f;

            public static float CirculationStrength = 0.35f;

            // ---- clamp response (a destroy or a move, refused) ----
            public static float ClampDuration = 0.32f;

            public static float ClampMembraneStretch = 0.5f;

            public static float ClampOppositeTighten = 0.35f;

            /// <summary>How far the cube loads into the force. It never leaves the cell.</summary>
            public static float ClampCubeLoad = 0.018f;

            public static float ClampRibTension = 1f;

            public static float ClampCoreCompression = 0.05f;

            public static float ClampShadowTighten = 0.1f;

            // ---- ground lock (a moving board, refused) ----
            public static float GroundLockLoad = 0.022f;

            public static float GroundLockNodeOffset = 0.012f;

            // ---- bond severance (the player's line) ----
            public static float RuptureFinalGrip = 0.1f;

            public static float RuptureFirstRib = 0.14f;

            public static float RuptureRibStagger = 0.045f;

            public static float RuptureRibBreak = 0.09f;

            public static float RuptureRibRetract = 0.075f;

            public static float RupturePeelDistance = 0.06f;

            public static float RuptureCoreReveal = 0.1f;

            public static float RuptureEssenceReveal = 1.22f;

            public static float RuptureEssenceHold = 0.075f;

            public static float RuptureEssenceCollapse = 0.14f;

            public static int RuptureFleckCount = 5;

            public static int CoreMoteCount = 4;

            public static float RuptureResidue = 0.16f;

            /// <summary>Once the first bonds are gone the heart is no longer held evenly: it is
            /// pulled toward whatever is still attached and leans.</summary>
            public static float RuptureCoreDrift = 0.03f;

            public static float RuptureShearWidth = 0.26f;

            public static float RuptureTotal = 0.78f;
        }

        /// <summary>What the lab can switch off one at a time.</summary>
        public static class Layers
        {
            public static bool ShowMembrane = true;

            public static bool ShowFolds = true;

            public static bool ShowWindows = true;

            public static bool ShowPatches = true;

            public static bool ShowVeins = true;

            /// <summary>What the lab is forcing the drain to, or below zero for the tuned value.
            /// It is here rather than in Style because it is a SWITCH, not a number anyone is
            /// meant to ship: the tiers exist so the withering can be judged at 0, a little, and
            /// all the way, and AllOn puts the real value back.</summary>
            public static float DrainOverride = -1f;

            public static bool ShowRibs = true;

            public static bool ShowCore = true;

            public static bool ShowEssence = true;

            public static bool ShowDeformation = true;

            public static bool ShowShadows = true;

            public static bool ShowFlecks = true;

            public static void AllOn()
            {
                ShowMembrane = true;
                ShowFolds = true;
                ShowWindows = true;
                ShowPatches = true;
                ShowVeins = true;
                ShowRibs = true;
                DrainOverride = -1f;
                ShowCore = true;
                ShowEssence = true;
                ShowDeformation = true;
                ShowShadows = true;
                ShowFlecks = true;
            }
        }

        /// <summary>The parasite's own palette - dark plum through bruised violet with one warm
        /// accent. NEVER a neon pink, never a full-cell magenta, and never the whole cube.</summary>
        public static readonly Color DeepPlum = new Color(0.12f, 0.06f, 0.14f);

        public static readonly Color BodyViolet = new Color(0.26f, 0.13f, 0.28f);

        public static readonly Color WarmRose = new Color(0.56f, 0.26f, 0.38f);

        /// <summary>What a passenger with no colour of its own gets: a muted warm amber that says
        /// "something is riding here". Never the parasite's own plum - that would say the socket is
        /// empty.</summary>
        public static readonly Color UnknownPassenger = new Color(0.78f, 0.6f, 0.3f);

        // =================================================================== what it is told

        /// <summary>One host cube on the board: where it is, and who is riding it.</summary>
        public struct Host
        {
            public GridPos Cell;
            public BoundJokerIdentity Passenger;
            /// <summary>The cube's OWN face. The drain layer redraws it desaturated under the
            /// wrap, which is the only way the block can be seen losing its colour LOCALLY - a
            /// tint on the cube would be a global recolour, and the whole point is that the film
            /// takes the life out of it only where it lies thick.</summary>
            public ClusterBurstView.Look Look;
        }

        /// <summary>An attempt the rules refused, ready for the View: the cell, what tried, and the
        /// direction it came from when Core knew one.</summary>
        public struct Refusal
        {
            public GridPos Cell;
            public HostRefusalKind Kind;
            public Vector2 Step;
            public bool HasDirection;
        }

        // =================================================================== material

        private static Material harnessMaterial;

        private static bool harnessLooked;

        /// <summary>The ParasiteHarness material, or null when the shader cannot be had - the
        /// harness is then drawn in flat plum, which still says a clasp is there.</summary>
        public static Material HarnessMaterial()
        {
            if (!harnessLooked)
            {
                harnessLooked = true;
                Shader shader = Shader.Find("ProjectBlock/ParasiteHarness");
                if (shader == null)
                {
                    shader = Resources.Load<Shader>("Shaders/ParasiteHarness");
                }
                if (shader != null && shader.isSupported)
                {
                    harnessMaterial = new Material(shader);
                    harnessMaterial.hideFlags = HideFlags.HideAndDontSave;
                }
            }
            return harnessMaterial;
        }

        private static readonly int DeepId = Shader.PropertyToID("_Deep");
        private static readonly int BodyId = Shader.PropertyToID("_Body");
        private static readonly int RoseId = Shader.PropertyToID("_Rose");
        private static readonly int ToneId = Shader.PropertyToID("_Tone");
        private static readonly int ReliefId = Shader.PropertyToID("_Relief");
        private static readonly int SinkId = Shader.PropertyToID("_Sink");
        private static readonly int TensionId = Shader.PropertyToID("_Tension");
        private static readonly int SheenId = Shader.PropertyToID("_Sheen");
        private static readonly int SeverId = Shader.PropertyToID("_Sever");
        private static readonly int RidgesId = Shader.PropertyToID("_Ridges");
        private static readonly int RimId = Shader.PropertyToID("_Rim");
        private static readonly int RimColourId = Shader.PropertyToID("_RimColour");
        private static readonly int FaceHalfId = Shader.PropertyToID("_FaceHalf");

        private static readonly Color Clear = new Color(1f, 1f, 1f, 0f);

        // Sorting: the board's own cube is under all of this. The film lies ON its face, the
        // binding strands ride inside the film, the core sits over them and the passenger is inside
        // the core.
        /// <summary>The drained copy of the cube's face sits directly over the board's own cube
        /// and UNDER the film, so the block is seen going pale beneath the wrap.</summary>
        private const int DrainOrder = 4;

        private const int ShadowOrder = 5;

        private const int MembraneOrder = 6;

        private const int PatchOrder = 8;

        private const int VeinOrder = 8;

        private const int RibOrder = 9;

        private const int CoreOrder = 10;

        /// <summary>The passenger is three shells, so each needs its own order - two sprites on the
        /// same one sort against each other by nothing in particular.</summary>
        private const int EssenceOuterOrder = 11;

        private const int EssenceOrder = 12;

        private const int EssenceInnerOrder = 13;

        private const int FleckOrder = 14;

        private static Material drainMaterial;

        private static bool drainLooked;

        /// <summary>The ParasiteDrain material, or null - the host then simply keeps its full
        /// colour, which costs the withering read and nothing else.</summary>
        public static Material DrainMaterial()
        {
            if (!drainLooked)
            {
                drainLooked = true;
                Shader shader = Shader.Find("ProjectBlock/ParasiteDrain");
                if (shader == null)
                {
                    shader = Resources.Load<Shader>("Shaders/ParasiteDrain");
                }
                if (shader != null && shader.isSupported)
                {
                    drainMaterial = new Material(shader);
                    drainMaterial.hideFlags = HideFlags.HideAndDontSave;
                }
            }
            return drainMaterial;
        }

        private static Material membraneMaterial;

        private static bool membraneLooked;

        /// <summary>The ParasiteMembrane material, or null when the shader cannot be had - the film
        /// is then not drawn at all and the strands and core still say something is wrapped round
        /// the cube.</summary>
        public static Material MembraneMaterial()
        {
            if (!membraneLooked)
            {
                membraneLooked = true;
                Shader shader = Shader.Find("ProjectBlock/ParasiteMembrane");
                if (shader == null)
                {
                    shader = Resources.Load<Shader>("Shaders/ParasiteMembrane");
                }
                if (shader != null && shader.isSupported)
                {
                    membraneMaterial = new Material(shader);
                    membraneMaterial.hideFlags = HideFlags.HideAndDontSave;
                }
            }
            return membraneMaterial;
        }

        private static readonly int OpacityId = Shader.PropertyToID("_Opacity");
        private static readonly int ThickId = Shader.PropertyToID("_Thick");
        private static readonly int ShapeId = Shader.PropertyToID("_Shape");
        private static readonly int BulgeId = Shader.PropertyToID("_Bulge");
        private static readonly int EdgeWrapId = Shader.PropertyToID("_EdgeWrap");
        private static readonly int TearId = Shader.PropertyToID("_Tear");
        private static readonly int SheenId2 = Shader.PropertyToID("_Sheen");
        private static readonly int VeinId = Shader.PropertyToID("_Vein");
        private static readonly int DrainId = Shader.PropertyToID("_Drain");
        private static readonly int DesatId = Shader.PropertyToID("_Desat");
        private static readonly int DimId = Shader.PropertyToID("_Dim");
        private static readonly int DeadId = Shader.PropertyToID("_Dead");
        private static readonly int ReliefId2 = Shader.PropertyToID("_Relief");
        private static readonly int StainId = Shader.PropertyToID("_Stain");
        private static readonly int KillId = Shader.PropertyToID("_Kill");
        private static readonly int CurveId = Shader.PropertyToID("_Curve");
        private static readonly int CurlId = Shader.PropertyToID("_Curl");
        private static readonly int FoldAId = Shader.PropertyToID("_FoldA");
        private static readonly int FoldBId = Shader.PropertyToID("_FoldB");
        private static readonly int FoldCId = Shader.PropertyToID("_FoldC");
        private static readonly int FoldReliefId = Shader.PropertyToID("_FoldRelief");
        private static readonly int WindowAId = Shader.PropertyToID("_WindowA");
        private static readonly int WindowBId = Shader.PropertyToID("_WindowB");

        // =================================================================== a standing host

        /// <summary>
        /// One BINDING STRAND. Its path crosses the face rather than running corner-to-centre, so
        /// two or three of them read as bands holding the cube down instead of a symmetric star.
        /// Drawn as a chain of capsule segments along a bezier, which is what lets it curve, taper,
        /// tension and - when the line comes - break in the middle with each half retracting.
        /// </summary>
        private sealed class Rib
        {
            public readonly List<SpriteRenderer> Segments = new List<SpriteRenderer>();
            /// <summary>Where it enters and leaves the face, in cube-relative units, and how far
            /// its middle bows off the straight line between them.</summary>
            public Vector2 From;
            public Vector2 To;
            public float Bow;
            /// <summary>Which segment it parts at once the wrap fails. Below zero while it holds.
            /// </summary>
            public int BreakAt = -1;
        }

        private sealed class HostPiece
        {
            public GridPos Cell;
            public BoundJokerIdentity Passenger;
            public readonly List<Rib> Ribs = new List<Rib>();
            /// <summary>The film's own GATHERS, as the shaders want them: direction in radians,
            /// how far across the face the ridge sits, how far it bows, and its half width. Three
            /// numbers and a width rather than a chain of pieces, because the fold is drawn by the
            /// membrane itself.</summary>
            public Vector4 FoldA;
            public Vector4 FoldB;
            public Vector4 FoldC;
            /// <summary>The two holes in the film, in UV: centre, and radius in w. The strength is
            /// applied at paint time so the lab can switch them off.</summary>
            public Vector4 WindowA;
            public Vector4 WindowB;
            /// <summary>The membrane is a MESH, not a sprite: the idle needs a local bulge and a
            /// quad has nowhere to put one.</summary>
            public MeshRenderer Membrane;
            public MeshFilter MembraneFilter;
            /// <summary>The cube's own face, redrawn drained under the film.</summary>
            public SpriteRenderer Drain;
            public ClusterBurstView.Look Look;
            /// <summary>THE PALETTE FOR THIS BLOCK. A dark plum parasite on obsidian is
            /// dark-on-dark and the whole organism disappears, so the contrast - never the hue
            /// family - is a function of what the host is made of (ParasiteContrastProfile).
            /// </summary>
            public ParasiteContrast Contrast;
            /// <summary>The dead thickenings, and where each one sits.</summary>
            public readonly List<SpriteRenderer> Patches = new List<SpriteRenderer>();
            public readonly List<Vector3> PatchPlacements = new List<Vector3>();
            public readonly List<float> PatchTurns = new List<float>();
            /// <summary>The thin curved veins inside the film - secondary detail.</summary>
            public readonly List<Rib> Veins = new List<Rib>();
            /// <summary>Where the last struggle happened and how long ago, so the wrap can be seen
            /// having taken a little more out of the cube there.</summary>
            public Vector2 StainAt = new Vector2(0.5f, 0.5f);
            public float StainClock = -1f;
            /// <summary>What the struggles have cost this block for good (up to a cap). It decays
            /// very slowly, so a host that has fought several times is visibly further gone than
            /// one that was seated a moment ago.</summary>
            public float StainDepth;
            /// <summary>The recapture payment: seconds into the surge, or below zero.</summary>
            public float WitherClock = -1f;
            public SpriteRenderer Core;
            public SpriteRenderer CoreShadow;
            public SpriteRenderer EssenceOuter;
            public SpriteRenderer Essence;
            public SpriteRenderer EssenceInner;
            public readonly List<SpriteRenderer> Flecks = new List<SpriteRenderer>();
            public readonly List<Vector2> FleckDirs = new List<Vector2>();
            /// <summary>Seconds since the block landed, or above SeatTotal once seated.</summary>
            public float SeatClock;
            /// <summary>The escape attempt: seconds in, and WHERE the cube is pushing (UV).</summary>
            public float IdleWait;
            public float IdleClock = -1f;
            public Vector2 BulgeAt = new Vector2(0.5f, 0.5f);
            public int BulgeRegion;
            /// <summary>The rarer circulation.</summary>
            public float CirculationWait;
            public float CirculationClock = -1f;
            /// <summary>The clamp a refusal is playing.</summary>
            public float ClampClock = -1f;
            public Vector2 ClampFrom;
            public bool ClampIsMove;
            /// <summary>Seconds into the rupture, or below zero.</summary>
            public float RuptureClock = -1f;
            public Vector2 TearDir = new Vector2(1f, 0f);
            /// <summary>The membrane's own contour turn, so two hosts are not the same shape.
            /// </summary>
            public float ShapeTurn;
        }

        /// <summary>Where the cube can push from. A preset set, chosen per attempt - never a new
        /// random point every frame, which would read as noise rather than as a creature.</summary>
        private static readonly Vector2[] BulgeRegions =
        {
            new Vector2(0.3f, 0.72f), new Vector2(0.72f, 0.68f), new Vector2(0.24f, 0.42f),
            new Vector2(0.78f, 0.36f), new Vector2(0.5f, 0.22f), new Vector2(0.46f, 0.8f)
        };

        private readonly Dictionary<GridPos, HostPiece> hosts = new Dictionary<GridPos, HostPiece>();

        private readonly List<GridPos> retired = new List<GridPos>();

        private MaterialPropertyBlock block;

        private float cellSize;

        private float cubeSize;

        private System.Func<GridPos, Vector2> toWorld;

        private void Awake()
        {
            block = new MaterialPropertyBlock();
        }

        /// <summary>True while any host is playing something the lab should wait on.</summary>
        public bool Busy
        {
            get
            {
                foreach (KeyValuePair<GridPos, HostPiece> entry in hosts)
                {
                    HostPiece h = entry.Value;
                    if (h.RuptureClock >= 0f || h.ClampClock >= 0f
                        || h.SeatClock < Style.SeatTotal)
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        // =================================================================== driving it

        /// <summary>
        /// The host cubes the rules currently have on the board, asked on every repaint so what is
        /// drawn is what the rules say rather than a memory of it. A cell that has stopped being a
        /// host loses its harness - unless it is in the middle of a severance, which is exactly the
        /// moment the cube goes away and the parasite has to be seen letting go of it.
        /// </summary>
        public void Sync(BoardView view, IReadOnlyList<Host> live)
        {
            if (view == null || view.Board == null)
            {
                return;
            }
            cellSize = view.CellWorldSize;
            cubeSize = view.CubeWorldSize;
            toWorld = view.CellToWorld;
            retired.Clear();
            foreach (KeyValuePair<GridPos, HostPiece> entry in hosts)
            {
                bool still = false;
                for (int i = 0; live != null && i < live.Count; i++)
                {
                    if (live[i].Cell.Equals(entry.Key))
                    {
                        still = true;
                        break;
                    }
                }
                if (!still && entry.Value.RuptureClock < 0f)
                {
                    retired.Add(entry.Key);
                }
            }
            for (int i = 0; i < retired.Count; i++)
            {
                Drop(retired[i]);
            }
            for (int i = 0; live != null && i < live.Count; i++)
            {
                HostPiece h;
                if (!hosts.TryGetValue(live[i].Cell, out h))
                {
                    h = Make(live[i]);
                    hosts[live[i].Cell] = h;
                }
                h.Passenger = live[i].Passenger;
            }
            PaintAll();
        }

        /// <summary>
        /// The rules refused an attempt on a host: it grips harder. Straight from
        /// GameBoard.HostRefusals - the side it clamps on is the side Core says the force came
        /// from, and an attempt with no direction gets an undirected clamp rather than a guessed
        /// side.
        /// </summary>
        public void PlayRefusal(Refusal refusal)
        {
            HostPiece h;
            if (!hosts.TryGetValue(refusal.Cell, out h) || h.RuptureClock >= 0f)
            {
                return;
            }
            h.ClampClock = 0f;
            h.ClampFrom = refusal.HasDirection && refusal.Step.sqrMagnitude > 0f
                ? refusal.Step.normalized
                : Vector2.zero;
            h.ClampIsMove = refusal.Kind == HostRefusalKind.ForcedMove;
        }

        /// <summary>
        /// The player's line reached a host: the one thing the parasite cannot hold. The bonds
        /// break one at a time, the passenger is shown, and both go.
        /// </summary>
        public void PlaySeverance(GridPos cell)
        {
            PlaySeverance(cell, true);
        }

        /// <summary>
        /// The same, told which way the line ran. The TEAR opens along that band - a row shears the
        /// wrap horizontally and a column vertically - so the failure is visibly caused by the line
        /// the player completed rather than happening near it. The orientation is Core's; the View
        /// never works out which line went off.
        /// </summary>
        public void PlaySeverance(GridPos cell, bool horizontal)
        {
            HostPiece h;
            if (!hosts.TryGetValue(cell, out h) || h.RuptureClock >= 0f)
            {
                return;
            }
            h.RuptureClock = 0f;
            h.ClampClock = -1f;
            h.IdleClock = -1f;
            h.CirculationClock = -1f;
            h.TearDir = horizontal ? new Vector2(1f, 0f) : new Vector2(0f, 1f);
            
            if (Layers.ShowFlecks)
            {
                int n = Mathf.Max(0, Style.RuptureFleckCount);
                for (int i = 0; i < n; i++)
                {
                    // Torn tissue, not confetti: the parasite's own capsule shape, small.
                    h.Flecks.Add(Rent(ParasiteShapes.TendrilSegment, FleckOrder, null));
                    float a = (i * 2.39996f) % (Mathf.PI * 2f);
                    h.FleckDirs.Add(new Vector2(Mathf.Cos(a), Mathf.Sin(a)));
                }
                int m = Mathf.Max(0, Style.CoreMoteCount);
                for (int i = 0; i < m; i++)
                {
                    h.Flecks.Add(Rent(ParasiteShapes.Essence, FleckOrder, null));
                    float a = (i * 1.7f + 0.6f) % (Mathf.PI * 2f);
                    h.FleckDirs.Add(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 0.6f);
                }
            }
        }

        /// <summary>A clean sweep passed over a host and could not take it: the node contracts and
        /// the bonds darken for a moment. Nothing else - "the sweep did not get this one".</summary>
        public void PlaySweepPass(GridPos cell)
        {
            HostPiece h;
            if (hosts.TryGetValue(cell, out h) && h.RuptureClock < 0f)
            {
                h.IdleClock = 0f;
            }
        }

        /// <summary>True while that cell carries a harness.</summary>
        public bool Holds(GridPos cell)
        {
            return hosts.ContainsKey(cell);
        }

        public void Stop()
        {
            retired.Clear();
            foreach (KeyValuePair<GridPos, HostPiece> entry in hosts)
            {
                retired.Add(entry.Key);
            }
            for (int i = 0; i < retired.Count; i++)
            {
                Drop(retired[i]);
            }
            hosts.Clear();
        }

        // =================================================================== building

        private HostPiece Make(Host host)
        {
            var h = new HostPiece { Cell = host.Cell, Passenger = host.Passenger };
            Material harness = HarnessMaterial();
            h.ShapeTurn = Random01(host.Cell, 11) * 6.28f;

            if (Layers.ShowMembrane)
            {
                Material film = MembraneMaterial();
                if (film != null)
                {
                    var go = new GameObject("Membrane");
                    go.transform.SetParent(transform, false);
                    h.MembraneFilter = go.AddComponent<MeshFilter>();
                    h.MembraneFilter.sharedMesh = ParasiteMembraneMesh.Quad;
                    h.Membrane = go.AddComponent<MeshRenderer>();
                    h.Membrane.sharedMaterial = film;
                    h.Membrane.sortingOrder = MembraneOrder;
                    h.Membrane.shadowCastingMode =
                        UnityEngine.Rendering.ShadowCastingMode.Off;
                    h.Membrane.receiveShadows = false;
                }
            }

            // THE STRANDS CROSS THE FACE. Their ends are on the rim but never on the corners, and
            // no two share a path - that is what keeps them bands rather than a star.
            h.Look = host.Look;
            h.Contrast = ParasiteContrastProfile.For(host.Look.Paint.a > 0f
                ? host.Look.Paint : host.Look.Colour);
            if (host.Look.Tile != null)
            {
                Material drain = DrainMaterial();
                if (drain != null)
                {
                    h.Drain = Rent(host.Look.Tile, DrainOrder, drain);
                }
            }

            // THE DEAD THICKENINGS. Two to four, from four baked variants, at stable places - the
            // same host is the same host every time it is drawn.
            int patchCount = Style.PatchCountMin
                + (int)(Random01(host.Cell, 90) * (Style.PatchCountMax - Style.PatchCountMin + 1));
            patchCount = Layers.ShowPatches
                ? Mathf.Clamp(patchCount, Style.PatchCountMin, Style.PatchCountMax)
                : 0;
            for (int i = 0; i < patchCount; i++)
            {
                int variant = (int)(Random01(host.Cell, 100 + i) * 4f) % 4;
                h.Patches.Add(Rent(ParasiteShapes.Patch(variant), PatchOrder, harness));
                float a = Random01(host.Cell, 120 + i) * 6.28f;
                float r = 0.12f + Random01(host.Cell, 140 + i) * 0.26f;
                float size = Mathf.Lerp(Style.PatchSizeMin, Style.PatchSizeMax,
                    Random01(host.Cell, 160 + i));
                h.PatchPlacements.Add(new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, size));
                h.PatchTurns.Add(Random01(host.Cell, 180 + i) * 360f);
            }

            // THE VEINS. Thin, curved, branching, and SECONDARY - they must never become the thing
            // the eye reads first, which is what the straight network in the first pass was.
            int veinCount = Layers.ShowVeins ? Style.VeinCount : 0;
            for (int i = 0; i < veinCount; i++)
            {
                var vein = new Rib();
                float a = Random01(host.Cell, 200 + i) * 6.28f;
                float len = 0.22f + Random01(host.Cell, 220 + i) * 0.24f;
                // Out of the nest, not between two rim points: a vein comes FROM the parasite.
                vein.From = Vector2.zero;
                vein.To = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * len;
                vein.Bow = Style.VeinCurve * (Random01(host.Cell, 240 + i) * 1.8f - 0.9f);
                int n = Mathf.Max(3, Style.VeinSegments);
                for (int k = 0; k < n; k++)
                {
                    vein.Segments.Add(Rent(ParasiteShapes.TendrilSegment, VeinOrder, harness));
                }
                h.Veins.Add(vein);
            }

            // THE THICK GATHERS. Where the film heaps up - stable per host, and passed to the
            // membrane and the drain so the heaviest wrap and the deepest withering agree.
            h.FoldA = FoldFor(host.Cell, 0);
            h.FoldB = FoldFor(host.Cell, 1);
            h.FoldC = FoldFor(host.Cell, 2);

            // THE HOLES. Two of them, off the middle (the nest is there) and stable per host.
            h.WindowA = WindowFor(host.Cell, 0);
            h.WindowB = WindowFor(host.Cell, 1);

            int ribs = Mathf.Clamp(Style.RibCount, 2, 4);
            for (int i = 0; i < ribs; i++)
            {
                var rib = new Rib();
                float a = Random01(host.Cell, 40 + i) * 6.28f + i * (6.28f / ribs);
                float b = a + 2.2f + Random01(host.Cell, 60 + i) * 1.6f;
                rib.From = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 0.52f;
                rib.To = new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * 0.52f;
                rib.Bow = Style.RibCurve * (Random01(host.Cell, 80 + i) * 1.6f - 0.6f);
                if (Layers.ShowRibs)
                {
                    int n = Mathf.Max(3, Style.RibSegments);
                    for (int k = 0; k < n; k++)
                    {
                        rib.Segments.Add(Rent(ParasiteShapes.TendrilSegment, RibOrder, harness));
                    }
                }
                h.Ribs.Add(rib);
            }

            if (Layers.ShowCore)
            {
                h.Core = Rent(ParasiteShapes.Heart, CoreOrder, harness);
                if (Layers.ShowShadows)
                {
                    h.CoreShadow = Rent(ParasiteShapes.Heart, ShadowOrder, null);
                }
            }
            if (Layers.ShowEssence)
            {
                // Three shells of the same seed: a dark husk, the joker's own colour, a pale
                // centre. One flat dot of a hue is a status light.
                h.EssenceOuter = Rent(ParasiteShapes.Essence, EssenceOuterOrder, null);
                h.Essence = Rent(ParasiteShapes.Essence, EssenceOrder, null);
                h.EssenceInner = Rent(ParasiteShapes.Essence, EssenceInnerOrder, null);
            }
            h.IdleWait = Style.IdleMinInterval
                + Random01(host.Cell, 5) * (Style.IdleMaxInterval - Style.IdleMinInterval);
            h.CirculationWait = Style.CirculationMinInterval
                + Random01(host.Cell, 6)
                    * (Style.CirculationMaxInterval - Style.CirculationMinInterval);
            return h;
        }

        /// <summary>One of the film's GATHERS on this host: which way the ridge runs, how far
        /// across the face it sits, how far it bows, and how wide it is. Beyond FoldCount the width
        /// is zero, which is how the shaders know there is no third gather here.</summary>
        private static Vector4 FoldFor(GridPos cell, int index)
        {
            if (index >= Mathf.Clamp(Style.FoldCount, 1, 3) || !Layers.ShowFolds)
            {
                return Vector4.zero;
            }
            // Spread round the face rather than parallel, so they cross as gathers in a wrapped
            // sheet do and never read as three stripes.
            float dir = Random01(cell, 300 + index) * 1.6f + index * 1.05f;
            float offset = (Random01(cell, 320 + index) - 0.5f) * 0.9f;
            float bow = Style.FoldCurve * (Random01(cell, 340 + index) * 1.6f - 0.8f);
            float width = Style.FoldWidth * (0.8f + Random01(cell, 360 + index) * 0.5f);
            return new Vector4(dir, offset, bow, width);
        }

        /// <summary>The same, tightened: a sheet pulled taut has fewer folds in it.</summary>
        private static Vector4 FoldVec(Vector4 fold, float tension, float open)
        {
            if (fold.w <= 0f)
            {
                return Vector4.zero;
            }
            float flatten = 1f - Style.FoldTensionFlatten * Mathf.Clamp01(tension);
            return new Vector4(fold.x, fold.y, fold.z * flatten,
                fold.w * flatten * Mathf.Clamp01(open));
        }

        /// <summary>Where one of the film's holes sits on this host, in UV. Off the middle, because
        /// the nest is there and a hole under it would only ever be a dark ring.</summary>
        private static Vector4 WindowFor(GridPos cell, int index)
        {
            float a = Random01(cell, 400 + index) * 6.28f + index * 3.1f;
            float r = 0.22f + Random01(cell, 420 + index) * 0.11f;
            float radius = Mathf.Lerp(Style.WindowRadiusMin, Style.WindowRadiusMax,
                Random01(cell, 440 + index));
            // NEVER A MATCHED PAIR: one is a tear the film has pulled open, the other a nick. Two
            // holes the same size on one face read as a pair of lights however torn their edges.
            radius *= index == 0 ? 1f : Style.WindowSecondScale;
            return new Vector4(0.5f + Mathf.Cos(a) * r, 0.5f + Mathf.Sin(a) * r, 0f, radius);
        }

        /// <summary>Where the core sits: near the middle, but never exactly on it.</summary>
        private static Vector2 CoreOffsetOf(HostPiece h)
        {
            float a = Random01(h.Cell, 21) * 6.28f;
            return new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Style.CoreOffset;
        }

        private void Drop(GridPos cell)
        {
            HostPiece h;
            if (!hosts.TryGetValue(cell, out h))
            {
                return;
            }
            for (int i = 0; i < h.Ribs.Count; i++)
            {
                for (int k = 0; k < h.Ribs[i].Segments.Count; k++)
                {
                    Return(h.Ribs[i].Segments[k]);
                }
                h.Ribs[i].Segments.Clear();
            }
            h.Ribs.Clear();
            for (int i = 0; i < h.Veins.Count; i++)
            {
                for (int k = 0; k < h.Veins[i].Segments.Count; k++)
                {
                    Return(h.Veins[i].Segments[k]);
                }
                h.Veins[i].Segments.Clear();
            }
            h.Veins.Clear();
            for (int i = 0; i < h.Patches.Count; i++)
            {
                Return(h.Patches[i]);
            }
            h.Patches.Clear();
            h.PatchPlacements.Clear();
            h.PatchTurns.Clear();
            Return(h.Drain);
            Return(h.Core);
            Return(h.CoreShadow);
            Return(h.EssenceOuter);
            Return(h.Essence);
            Return(h.EssenceInner);
            for (int i = 0; i < h.Flecks.Count; i++)
            {
                Return(h.Flecks[i]);
            }
            h.Flecks.Clear();
            h.FleckDirs.Clear();
            if (h.Membrane != null)
            {
                Destroy(h.Membrane.gameObject);
                h.Membrane = null;
            }
            hosts.Remove(cell);
        }

        // =================================================================== the clock

        private void Update()
        {
            if (hosts.Count == 0)
            {
                return;
            }
            float dt = Time.deltaTime;
            retired.Clear();
            foreach (KeyValuePair<GridPos, HostPiece> entry in hosts)
            {
                HostPiece h = entry.Value;
                h.SeatClock += dt;
                if (h.RuptureClock >= 0f)
                {
                    h.RuptureClock += dt;
                    if (h.RuptureClock > Style.RuptureTotal)
                    {
                        retired.Add(entry.Key);
                    }
                    continue;
                }
                if (h.ClampClock >= 0f)
                {
                    h.ClampClock += dt;
                    if (h.ClampClock > Style.ClampDuration)
                    {
                        h.ClampClock = -1f;
                    }
                }
                // THE ESCAPE ATTEMPT. Rare, and it runs to its end once it starts - a cube that
                // wriggled continuously would be a creature, and the cube is not the creature.
                if (h.IdleClock >= 0f)
                {
                    h.IdleClock += dt;
                    if (h.IdleClock > Style.IdleDuration)
                    {
                        h.IdleClock = -1f;
                        // IT COST SOMETHING. The region it struggled in is left a little more
                        // drained for a moment, which is what makes the idle a losing fight rather
                        // than a fidget.
                        h.StainClock = 0f;
                        // AND THE WRAP TAKES ITS PAYMENT. The whole face withers for a moment as
                        // the cube is pressed back down, and the region it fought in keeps a
                        // little of that for good.
                        h.WitherClock = 0f;
                        h.StainDepth = Mathf.Min(Style.AfterDrainMax,
                            h.StainDepth + Style.AfterDrainGain);
                        h.IdleWait = Style.IdleMinInterval
                            + Random01(entry.Key, Mathf.RoundToInt(Time.time * 7f))
                                * (Style.IdleMaxInterval - Style.IdleMinInterval);
                    }
                }
                else if (h.SeatClock >= Style.SeatTotal && h.ClampClock < 0f)
                {
                    h.IdleWait -= dt;
                    if (h.IdleWait <= 0f)
                    {
                        h.IdleClock = 0f;
                        // A preset region, stepped rather than re-rolled, so the same cube does not
                        // push from the same place twice running.
                        h.BulgeRegion = (h.BulgeRegion + 1 + (int)(Random01(entry.Key,
                            Mathf.RoundToInt(Time.time * 13f)) * 3f)) % BulgeRegions.Length;
                        h.BulgeAt = BulgeRegions[h.BulgeRegion];
                        h.StainAt = h.BulgeAt;
                    }
                }
                // The rarer, quieter one.
                if (h.StainClock >= 0f)
                {
                    h.StainClock += dt;
                    if (h.StainClock > Style.AfterDrainDuration)
                    {
                        h.StainClock = -1f;
                    }
                }
                if (h.WitherClock >= 0f)
                {
                    h.WitherClock += dt;
                    if (h.WitherClock > Style.WitherPulseDuration)
                    {
                        h.WitherClock = -1f;
                    }
                }
                // The scar fades, but only just: what the struggles cost stands for a long while.
                if (h.StainDepth > 0f)
                {
                    h.StainDepth = Mathf.Max(0f, h.StainDepth - Style.AfterDrainDecay * dt);
                }
                if (h.CirculationClock >= 0f)
                {
                    h.CirculationClock += dt;
                    if (h.CirculationClock > Style.CirculationDuration)
                    {
                        h.CirculationClock = -1f;
                        h.CirculationWait = Style.CirculationMinInterval
                            + Random01(entry.Key, Mathf.RoundToInt(Time.time * 3f))
                                * (Style.CirculationMaxInterval - Style.CirculationMinInterval);
                    }
                }
                else if (h.SeatClock >= Style.SeatTotal && h.IdleClock < 0f)
                {
                    h.CirculationWait -= dt;
                    if (h.CirculationWait <= 0f)
                    {
                        h.CirculationClock = 0f;
                    }
                }
            }
            for (int i = 0; i < retired.Count; i++)
            {
                Drop(retired[i]);
            }
            PaintAll();
        }

        /// <summary>A deterministic 0..1 from a cell and a salt - no Random, so a board full of
        /// hosts is reproducible frame for frame in the lab.</summary>
        private static float Random01(GridPos cell, int salt)
        {
            uint v = (uint)(cell.X * 73856093 ^ cell.Y * 19349663 ^ salt * 83492791);
            v ^= v >> 13;
            v *= 1274126177u;
            v ^= v >> 16;
            return (v & 0xFFFFFF) / (float)0xFFFFFF;
        }

        private static float Span(float t, float a, float b)
        {
            if (b <= a)
            {
                return t >= b ? 1f : 0f;
            }
            return Mathf.Clamp01((t - a) / (b - a));
        }

        private static float Ease(float k)
        {
            k = Mathf.Clamp01(k);
            return k * k * (3f - 2f * k);
        }

        // =================================================================== painting

        private void PaintAll()
        {
            if (toWorld == null)
            {
                return;
            }
            foreach (KeyValuePair<GridPos, HostPiece> entry in hosts)
            {
                Paint(entry.Value);
            }
        }

        private void Paint(HostPiece h)
        {
            Vector2 at = toWorld(h.Cell);
            float cube = cubeSize > 0f ? cubeSize : cellSize;

            // ---- what the wrap is doing this frame ----
            float seat = Span(h.SeatClock, 0f, Style.SeatTotal);
            float rupture = h.RuptureClock >= 0f ? h.RuptureClock : -1f;

            // THE ESCAPE ATTEMPT, in four beats: pressure builds under the film, the cube pushes
            // out, the parasite clamps down, everything settles.
            float push = 0f;
            float restrain = 0f;
            if (h.IdleClock >= 0f)
            {
                float k = Mathf.Clamp01(h.IdleClock / Style.IdleDuration);
                // Build to a short peak at a third, then the wrap wins and presses it back.
                push = k < 0.35f ? Ease(k / 0.35f) : 1f - Ease((k - 0.35f) / 0.32f);
                push = Mathf.Clamp01(push);
                restrain = k > 0.4f ? Ease(Mathf.Clamp01((k - 0.4f) / 0.35f))
                    * (1f - Ease(Mathf.Clamp01((k - 0.75f) / 0.25f))) : 0f;
            }
            float clamp = 0f;
            if (h.ClampClock >= 0f)
            {
                float k = Mathf.Clamp01(h.ClampClock / Style.ClampDuration);
                clamp = k < 0.3f ? Ease(k / 0.3f) : 1f - Ease((k - 0.3f) / 0.7f);
            }
            float circulate = h.CirculationClock >= 0f
                ? Mathf.Sin(Mathf.Clamp01(h.CirculationClock / Style.CirculationDuration)
                    * Mathf.PI)
                : 0f;
            // The final grip before the line lands, and the tightening a clamp is.
            float grip = clamp;
            if (rupture >= 0f)
            {
                grip = Mathf.Max(grip, Ease(Span(rupture, 0f, Style.RuptureFinalGrip)));
            }
            grip = Mathf.Max(grip, restrain);

            // ---- where the cube is pushing from, and where it loads ----
            // The bulge is in UV; the direction it pushes is that, in cube space.
            Vector2 bulgeDir = (h.BulgeAt - new Vector2(0.5f, 0.5f)) * 2f;
            if (bulgeDir.sqrMagnitude > 1e-5f)
            {
                bulgeDir = bulgeDir.normalized;
            }
            Vector2 load = bulgeDir * (Style.IdleCubeLoad * cube * push);
            if (clamp > 0f && h.ClampFrom.sqrMagnitude > 0f)
            {
                float amount = h.ClampIsMove ? Style.GroundLockLoad : Style.ClampCubeLoad;
                load += h.ClampFrom * (amount * cube * clamp);
            }
            if (rupture >= 0f)
            {
                // One last push from underneath as the line arrives - it nearly gets out.
                load += bulgeDir * (Style.IdleCubeLoad * cube * 1.4f
                    * Ease(Span(rupture, 0f, Style.RuptureFinalGrip)));
            }
            Vector2 centre = at + load;

            PaintDrain(h, centre, cube, push, rupture);
            PaintMembrane(h, centre, cube, seat, push, grip, circulate, clamp, rupture);
            PaintPatches(h, centre, cube, grip, rupture);
            for (int i = 0; i < h.Veins.Count; i++)
            {
                PaintVein(h, i, centre, cube, push, grip, circulate, rupture);
            }
            for (int i = 0; i < h.Ribs.Count; i++)
            {
                PaintRib(h, i, centre, cube, push, grip, circulate, rupture);
            }
            PaintCore(h, centre, cube, seat, push, grip, circulate, clamp, rupture);
            PaintFlecks(h, at, cube, rupture);
        }

        /// <summary>One of the film's holes as the shaders want it: centre, strength, radius. The
        /// strength is applied here rather than baked in, so the lab can take the negative space
        /// away and the holes can open with the film as it spreads.</summary>
        private static Vector4 WindowVec(Vector4 w, float open)
        {
            return new Vector4(w.x, w.y,
                Layers.ShowWindows ? Style.WindowStrength * Mathf.Clamp01(open) : 0f, w.w);
        }

        /// <summary>
        /// THE COLOUR GOING OUT OF THE BLOCK. The cube's own face redrawn desaturated and dimmed,
        /// masked to the wrap's coverage and following its thickness - so the block is palest where
        /// the film lies heaviest and keeps its colour where it is thin. Where the cube pushes the
        /// film out its colour comes back for a moment; where it has just struggled and lost, a
        /// little more of it is gone.
        /// </summary>
        private void PaintDrain(HostPiece h, Vector2 at, float cube, float push, float rupture)
        {
            if (h.Drain == null)
            {
                return;
            }
            float spread = Ease(Span(h.SeatClock, Style.SeatCoreWake * 0.7f,
                Style.SeatCoreWake * 0.7f + Style.SeatMembraneSpread));
            // AS THE WRAP FAILS THE COLOUR COMES BACK - and it comes back SNAPPED, on the frames
            // the tear itself opens rather than over the rest of the sequence. That is the whole
            // micro-beat: the block is briefly its own colour again, alive, a moment before the
            // line takes it. Faded in slowly instead, it reads as the effect switching off.
            float release = rupture >= 0f
                ? Ease(Span(rupture, Style.RuptureFinalGrip,
                    Style.RuptureFinalGrip + Style.DeathColourReturn))
                : 0f;
            // THE RECAPTURE PAYMENT: the whole face withers for a moment as the wrap presses the
            // cube back down. Not the struggling region - all of it.
            float wither = h.WitherClock >= 0f && Style.WitherPulseDuration > 0f
                ? Mathf.Sin(Mathf.Clamp01(h.WitherClock / Style.WitherPulseDuration) * Mathf.PI)
                : 0f;
            h.Drain.transform.localPosition = new Vector3(at.x, at.y, 0f);
            Fit(h.Drain, cube, cube);
            h.Drain.color = h.Look.Colour;
            block.Clear();
            // Scaled by the block: a near-black cube has almost no colour left to take, and
            // draining it hard only muddies the cell.
            float strength = Layers.DrainOverride >= 0f
                ? Layers.DrainOverride
                : Style.DrainStrength;
            block.SetFloat(DrainId,
                strength * h.Contrast.DrainScale * spread * (1f - release)
                    * (1f + Style.WitherPulseStrength * wither));
            block.SetFloat(DesatId, Style.DrainSaturationLoss);
            block.SetFloat(DimId, Style.DrainBrightnessLoss);
            block.SetColor(DeadId, Style.DrainDead);
            block.SetFloat(KillId, Style.DrainHighlightKill);
            block.SetFloat(CurveId, Style.DrainCoverageCurve);
            block.SetVector(ShapeId, new Vector4(Style.MembraneReach, Style.MembraneLobes,
                h.ShapeTurn, Style.MembraneEdgeSoftness));
            block.SetFloat(ThickId, Style.MembraneThickness);
            block.SetVector(ReliefId2, new Vector4(h.BulgeAt.x, h.BulgeAt.y, push,
                Style.IdleBulgeRadius));
            // The struggle's immediate cost, plus what every struggle before it left for good.
            float stain = h.StainClock >= 0f && Style.AfterDrainDuration > 0f
                ? Style.AfterDrainStrength
                    * (1f - Mathf.Clamp01(h.StainClock / Style.AfterDrainDuration))
                : 0f;
            stain = Mathf.Clamp01(stain + h.StainDepth);
            block.SetVector(StainId, new Vector4(h.StainAt.x, h.StainAt.y, stain,
                Style.IdleBulgeRadius));
            // THE HOLES KEEP THEIR COLOUR: nothing is on the block there, so nothing is being
            // taken out of it there.
            block.SetVector(WindowAId, WindowVec(h.WindowA, spread));
            block.SetVector(WindowBId, WindowVec(h.WindowB, spread));
            // The block is deadest under the film's own gathers - the same three the membrane
            // draws, so the heaviest wrap and the deepest withering are in the same places.
            block.SetVector(FoldAId, FoldVec(h.FoldA, 0f, spread));
            block.SetVector(FoldBId, FoldVec(h.FoldB, 0f, spread));
            block.SetVector(FoldCId, FoldVec(h.FoldC, 0f, spread));
            h.Drain.SetPropertyBlock(block);
        }

        /// <summary>The dead thickenings: dark, matte, almost unlit. They are what stop the wrap
        /// reading as clean film - the places where it has died onto the cube.</summary>
        private void PaintPatches(HostPiece h, Vector2 at, float cube, float grip, float rupture)
        {
            for (int i = 0; i < h.Patches.Count; i++)
            {
                SpriteRenderer r = h.Patches[i];
                if (r == null)
                {
                    continue;
                }
                Vector3 place = h.PatchPlacements[i];
                float appear = Ease(Span(h.SeatClock,
                    Style.SeatCoreWake + Style.SeatMembraneSpread * 0.5f + i * 0.03f,
                    Style.SeatCoreWake + Style.SeatMembraneSpread + i * 0.03f));
                // A patch barely answers anything - it is dead tissue. It only comes away at the
                // end, when the line takes the wrap off the cube.
                float detach = rupture >= 0f && i < 3
                    ? Ease(Span(rupture, Style.RuptureFirstRib + i * Style.RuptureRibStagger,
                        Style.RuptureFirstRib + i * Style.RuptureRibStagger + 0.16f))
                    : 0f;
                Vector2 outward = new Vector2(place.x, place.y);
                outward = outward.sqrMagnitude > 1e-5f ? outward.normalized : Vector2.up;
                Vector2 p = at + new Vector2(place.x, place.y) * cube
                    + outward * (detach * cube * 0.07f);
                r.transform.localPosition = new Vector3(p.x, p.y, 0f);
                r.transform.localRotation =
                    Quaternion.Euler(0f, 0f, h.PatchTurns[i] + detach * 9f);
                float size = place.z * cube * (1f - grip * 0.03f);
                Fit(r, size, size * 0.9f);
                // Matte, nearly unlit, and DARK: no relief and no sheen. Its own CURLED EDGE is
                // lit instead, and on a dark block that edge is the only thing separating a dead
                // patch from the cube it died on - so the profile lifts it there and leaves it
                // almost off everywhere else.
                ParasiteContrast pc = h.Contrast;
                pc.Skin = pc.Patch;
                pc.Fold = pc.Patch;
                pc.Edge = pc.PatchEdge;
                pc.RimStrength = Mathf.Max(0.22f, h.Contrast.RimStrength);
                PaintHarness(r, 0.12f, 0.2f, 0f, 0f, -1f, -1f,
                    Style.PatchOpacity * appear * (1f - detach), pc);
            }
        }

        /// <summary>A vein: thin, curved, out of the nest into the film. SECONDARY - it must never
        /// become the thing the eye reads first, which is what the straight network was.</summary>
        private void PaintVein(HostPiece h, int index, Vector2 centre, float cube, float push,
            float grip, float circulate, float rupture)
        {
            Rib vein = h.Veins[index];
            if (vein.Segments.Count == 0)
            {
                return;
            }
            int n = vein.Segments.Count;
            float form = Ease(Span(h.SeatClock,
                Style.SeatCoreWake + Style.SeatMembraneSpread * 0.4f + index * 0.02f,
                Style.SeatCoreWake + Style.SeatMembraneSpread + index * 0.02f));
            Vector2 a = centre + vein.From * cube;
            Vector2 b = centre + vein.To * cube;
            Vector2 mid = (a + b) * 0.5f;
            Vector2 d = b - a;
            Vector2 normal = new Vector2(-d.y, d.x).normalized;
            float tension = Mathf.Max(grip * 0.6f, push * 0.5f);
            Vector2 control = mid + normal * (vein.Bow * cube * (1f - tension * 0.6f));
            float fail = rupture >= 0f
                ? Span(rupture, Style.RuptureFirstRib + index * 0.02f,
                    Style.RuptureFirstRib + index * 0.02f + Style.RuptureRibBreak)
                : 0f;
            for (int k = 0; k < n; k++)
            {
                SpriteRenderer r = vein.Segments[k];
                if (r == null)
                {
                    continue;
                }
                float t = n == 1 ? 0f : k / (float)(n - 1);
                if (t > form)
                {
                    r.color = Clear;
                    continue;
                }
                Vector2 p = Bezier(a, control, b, Mathf.Clamp01(t - fail * 0.3f));
                Vector2 tangent = BezierTangent(a, control, b, t);
                // Tapering to nothing at its far end - a vein, not a wire.
                float w = Style.VeinWidth * cube * (1f - t * 0.6f) * (1f - tension * 0.25f);
                float len = (b - a).magnitude / Mathf.Max(1, n - 1) * 2.6f;
                r.transform.localPosition = new Vector3(p.x, p.y, 0f);
                r.transform.localRotation = Quaternion.Euler(0f, 0f,
                    Mathf.Atan2(tangent.y, tangent.x) * Mathf.Rad2Deg - 90f);
                Fit(r, w, len);
                float sheen = circulate > 0f ? Mathf.Clamp01(1f - Mathf.Abs(circulate - t)) : -1f;
                PaintHarness(r, 0.7f, 0.4f, 0.3f, tension, sheen, -1f,
                    Style.VeinOpacity * form * (1f - Ease(fail)), h.Contrast);
            }
        }

        /// <summary>
        /// The film. Everything about it that changes is a shader property on one shared mesh: where
        /// the cube is pushing out from, how hard the wrap is pulling in, how far it spills over the
        /// edge, and - at the end - the tear the line opens along its own band.
        /// </summary>
        private void PaintMembrane(HostPiece h, Vector2 at, float cube, float seat, float push,
            float grip, float circulate, float clamp, float rupture)
        {
            if (h.Membrane == null)
            {
                return;
            }
            // It SPREADS from the core rather than appearing: the contour reaches out over the face.
            float spread = Ease(Span(h.SeatClock, Style.SeatCoreWake * 0.7f,
                Style.SeatCoreWake * 0.7f + Style.SeatMembraneSpread));
            // And it PEELS BACK toward the core as the wrap fails.
            float peel = rupture >= 0f
                ? Ease(Span(rupture, Style.RuptureFirstRib,
                    Style.RuptureFirstRib + Style.RuptureCoreReveal)) * Style.RupturePeelDistance
                : 0f;
            float size = cube * Style.MembraneOversize;
            h.Membrane.transform.localPosition = new Vector3(at.x, at.y, 0f);
            h.Membrane.transform.localScale = new Vector3(size, size, 1f);

            block.Clear();
            block.SetColor(DeepId, h.Contrast.Fold);
            block.SetColor(BodyId, h.Contrast.Skin);
            block.SetColor(RoseId, h.Contrast.Vein);
            block.SetFloat(RimId, h.Contrast.RimStrength);
            block.SetColor(RimColourId, h.Contrast.Edge);
            block.SetFloat(OpacityId, Style.MembraneOpacity * spread
                * (1f - (rupture >= 0f
                    ? Ease(Span(rupture, Style.RuptureTotal - 0.34f,
                        Style.RuptureTotal - 0.12f))
                    : 0f)));
            block.SetFloat(ThickId, Style.MembraneThickness);
            block.SetVector(ShapeId, new Vector4(
                (Style.MembraneReach - peel) * Mathf.Lerp(0.55f, 1f, spread),
                Style.MembraneLobes, h.ShapeTurn, Style.MembraneEdgeSoftness));
            // The bulge only exists while something is pushing, and the lab can switch it off.
            float bulge = Layers.ShowDeformation ? Style.IdleBulgeStrength * push : 0f;
            if (rupture >= 0f && Layers.ShowDeformation)
            {
                bulge = Mathf.Max(bulge, Style.IdleBulgeStrength * 1.3f
                    * Ease(Span(rupture, 0f, Style.RuptureFinalGrip))
                    * (1f - Ease(Span(rupture, Style.RuptureFirstRib,
                        Style.RuptureFirstRib + Style.RuptureRibBreak))));
            }
            block.SetVector(BulgeId, new Vector4(h.BulgeAt.x, h.BulgeAt.y, bulge,
                Style.IdleBulgeRadius));
            // A clamp STRETCHES the film toward the force and tightens it away from it; the
            // restraint beat just tightens.
            float tension = grip;
            if (clamp > 0f && h.ClampFrom.sqrMagnitude > 0f)
            {
                tension = Mathf.Max(tension, clamp * Style.ClampOppositeTighten
                    + clamp * Style.ClampMembraneStretch * 0.3f);
            }
            block.SetFloat(TensionId, tension);
            block.SetFloat(EdgeWrapId, Style.MembraneEdgeWrap
                * Ease(Span(h.SeatClock,
                    Style.SeatCoreWake + Style.SeatMembraneSpread,
                    Style.SeatCoreWake + Style.SeatMembraneSpread + Style.SeatEdgeGrip)));
            block.SetFloat(SheenId2, Style.MembraneSheen);
            // THE EDGE CURLS UNEVENLY, and the film has HOLES in it. Both exist to stop a
            // translucent sheet over a whole face reading as a filter on the cell: one gives the
            // boundary somewhere it grips and somewhere it does not, the other makes the cube and
            // the thing on it two objects.
            // Not a switch: an edge band of the same weight all the way round IS the border
            // this replaced, so the curl is on whenever there is a film at all.
            block.SetFloat(CurlId, 1f);
            // THE GATHERS, flattened by however hard the wrap is pulling: a sheet drawn taut has
            // fewer folds in it, and that is most of what makes a clamp read as a clamp.
            block.SetVector(FoldAId, FoldVec(h.FoldA, tension, spread));
            block.SetVector(FoldBId, FoldVec(h.FoldB, tension, spread));
            block.SetVector(FoldCId, FoldVec(h.FoldC, tension, spread));
            block.SetFloat(FoldReliefId, Layers.ShowFolds ? Style.FoldRelief : 0f);
            block.SetVector(WindowAId, WindowVec(h.WindowA, spread));
            block.SetVector(WindowBId, WindowVec(h.WindowB, spread));
            block.SetFloat(VeinId, circulate * Style.CirculationStrength);
            // THE TEAR: along the line's own band, and only once the line has arrived.
            float tear = rupture >= 0f
                ? Ease(Span(rupture, Style.RuptureFinalGrip,
                    Style.RuptureFinalGrip + Style.RuptureRibBreak * 2f))
                : 0f;
            block.SetVector(TearId, new Vector4(h.TearDir.x, h.TearDir.y, tear,
                Style.RuptureShearWidth));
            h.Membrane.SetPropertyBlock(block);
        }

        /// <summary>
        /// One binding strand: a chain of capsule segments along a bezier that CROSSES the face.
        /// Tension straightens it; the escape attempt bows the ones nearest the bulge; the line
        /// breaks it in the middle and each half retracts to its own end.
        /// </summary>
        private void PaintRib(HostPiece h, int index, Vector2 centre, float cube, float push,
            float grip, float circulate, float rupture)
        {
            Rib rib = h.Ribs[index];
            if (rib.Segments.Count == 0)
            {
                return;
            }
            int n = rib.Segments.Count;
            float form = Ease(Span(h.SeatClock,
                Style.SeatCoreWake + Style.SeatMembraneSpread * 0.6f
                    + index * Style.SeatRibStagger,
                Style.SeatCoreWake + Style.SeatMembraneSpread * 0.6f
                    + index * Style.SeatRibStagger + Style.SeatRibForm));
            Vector2 a = centre + rib.From * cube;
            Vector2 b = centre + rib.To * cube;
            Vector2 mid = (a + b) * 0.5f;
            Vector2 d = b - a;
            Vector2 normal = new Vector2(-d.y, d.x).normalized;
            // How much THIS strand is answering: the ones the bulge is under take the most.
            Vector2 bulgeWorld = centre
                + new Vector2(h.BulgeAt.x - 0.5f, h.BulgeAt.y - 0.5f) * 2f * cube * 0.5f;
            float near = 1f - Mathf.Clamp01((bulgeWorld - mid).magnitude / (cube * 0.7f));
            float tension = Mathf.Max(grip, push * near * Style.IdleRibTension);
            // Tension STRAIGHTENS a band; slack lets it bow.
            Vector2 control = mid + normal * (rib.Bow * cube * (1f - tension * 0.7f));
            // The bulge also pushes the strands over it outward a little.
            control += (mid - bulgeWorld).normalized * (push * near * cube * 0.05f);

            if (rib.BreakAt < 0 && rupture >= 0f
                && rupture > Style.RuptureFirstRib + index * Style.RuptureRibStagger)
            {
                rib.BreakAt = Mathf.Clamp(
                    Mathf.RoundToInt(n * (0.4f + 0.2f * ((index * 5 % 4) / 3f))), 1, n - 2);
            }
            float breakAtT = rupture >= 0f
                ? Span(rupture, Style.RuptureFirstRib + index * Style.RuptureRibStagger,
                    Style.RuptureFirstRib + index * Style.RuptureRibStagger
                        + Style.RuptureRibBreak)
                : -1f;

            for (int k = 0; k < n; k++)
            {
                SpriteRenderer r = rib.Segments[k];
                if (r == null)
                {
                    continue;
                }
                float t = n == 1 ? 0f : k / (float)(n - 1);
                // It rises out of the film from its middle outward.
                float grown = 1f - Mathf.Abs(t - 0.5f) * 2f;
                if (grown > form)
                {
                    r.color = Clear;
                    continue;
                }
                float slide = 0f;
                float fade = 1f;
                if (rib.BreakAt >= 0 && breakAtT >= 0f)
                {
                    float pull = Ease(breakAtT);
                    slide = (k >= rib.BreakAt ? 1f : -1f) * pull * 0.2f;
                    fade = 1f - Mathf.Clamp01((pull - 0.6f) / 0.4f);
                    if (k == rib.BreakAt || k == rib.BreakAt - 1)
                    {
                        // The two at the break thin to nothing first - that is the strand parting
                        // rather than vanishing.
                        fade *= 1f - Mathf.Clamp01(pull * 1.7f);
                    }
                }
                float u = Mathf.Clamp01(t + slide);
                Vector2 p = Bezier(a, control, b, u);
                Vector2 tangent = BezierTangent(a, control, b, u);
                // Thicker through the middle, tapering to its ends.
                float w = Mathf.Lerp(Style.RibEndWidth, Style.RibMidWidth,
                    1f - Mathf.Abs(u - 0.5f) * 2f) * cube * (1f - tension * 0.2f);
                // GENEROUSLY OVERLAPPED. Each segment has its own soft contour, so a chain laid
                // end to end beads like a caterpillar; at better than half an overlap the union is
                // one smooth band and only the taper shows.
                float len = (b - a).magnitude / Mathf.Max(1, n - 1) * 2.6f;
                r.transform.localPosition = new Vector3(p.x, p.y, 0f);
                r.transform.localRotation = Quaternion.Euler(0f, 0f,
                    Mathf.Atan2(tangent.y, tangent.x) * Mathf.Rad2Deg - 90f);
                Fit(r, w, len);
                float sheen = circulate > 0f && index == 0
                    ? Mathf.Clamp01(1f - Mathf.Abs(circulate - u))
                    : -1f;
                PaintHarness(r, 0.55f, 0.85f, 0.25f, tension, sheen, -1f,
                    Style.RibOpacity * Mathf.Clamp01(fade) * form, h.Contrast);
            }
        }

        private static Vector2 Bezier(Vector2 a, Vector2 b, Vector2 c, float t)
        {
            float u = 1f - t;
            return u * u * a + 2f * u * t * b + t * t * c;
        }

        private static Vector2 BezierTangent(Vector2 a, Vector2 b, Vector2 c, float t)
        {
            Vector2 d = 2f * (1f - t) * (b - a) + 2f * t * (c - b);
            return d.sqrMagnitude > 1e-6f ? d.normalized : (c - a).normalized;
        }

        /// <summary>The nest core, and the passenger inside it.</summary>
        private void PaintCore(HostPiece h, Vector2 centre, float cube, float seat, float push,
            float grip, float circulate, float clamp, float rupture)
        {
            Vector2 offset = CoreOffsetOf(h) * cube;
            // IT PULLS THE OTHER WAY. When the cube pushes out on one side the core is dragged
            // against it - which is what makes the parasite read as holding on rather than riding.
            Vector2 bulgeDir = (h.BulgeAt - new Vector2(0.5f, 0.5f)) * 2f;
            if (bulgeDir.sqrMagnitude > 1e-5f)
            {
                offset -= bulgeDir.normalized * (Style.IdleCoreCounter * cube * push);
            }
            if (clamp > 0f && h.ClampFrom.sqrMagnitude > 0f)
            {
                offset -= h.ClampFrom * (Style.GroundLockNodeOffset * cube * clamp);
            }
            if (rupture >= 0f)
            {
                offset += new Vector2(0f, Style.RuptureCoreDrift * cube
                    * Ease(Span(rupture, Style.RuptureFirstRib, Style.RuptureTotal * 0.7f)));
            }
            Vector2 at = centre + offset;

            float wake = Ease(Span(h.SeatClock, 0f, Style.SeatCoreWake));
            float grow = Mathf.Lerp(Style.SeatCoreStartScale, 1f, wake);
            float squeeze = 1f - Style.ClampCoreCompression * Mathf.Max(clamp, grip * 0.6f);
            float open = rupture >= 0f
                ? Ease(Span(rupture, Style.RuptureFirstRib + Style.RuptureRibStagger,
                    Style.RuptureFirstRib + Style.RuptureRibStagger + Style.RuptureCoreReveal))
                : 0f;
            float w = Style.CoreSize * cube * grow * squeeze * (1f + open * 0.2f);
            float hh = Style.CoreSize * cube * grow / Mathf.Max(squeeze, 0.01f)
                * (1f - open * 0.26f);
            if (h.Core != null)
            {
                h.Core.transform.localPosition = new Vector3(at.x, at.y, 0f);
                Fit(h.Core, w, hh);
                float alpha = wake * (1f - (rupture >= 0f
                    ? Ease(Span(rupture, Style.RuptureTotal - 0.24f, Style.RuptureTotal - 0.06f))
                    : 0f));
                // THE NEST IS A SHELL, not a bead. Growth ridges run across it, and it carries
                // its OWN contour light - a small dark body in the middle of a dark block is the
                // one piece of this that disappears first, and the ridges are no use if the
                // silhouette they are on cannot be found.
                ParasiteContrast nest = h.Contrast;
                nest.Edge = h.Contrast.NestRim;
                nest.RimStrength = Mathf.Max(Style.CoreRimBoost, h.Contrast.RimStrength);
                PaintHarness(h.Core, 0.9f, 1f, 0f, Mathf.Max(grip, clamp), -1f, -1f, alpha,
                    nest, Style.CoreRidges * wake);
            }
            if (h.CoreShadow != null)
            {
                h.CoreShadow.transform.localPosition =
                    new Vector3(at.x, at.y - cube * 0.022f, 0f);
                Fit(h.CoreShadow, w * 1.06f, hh * 1.02f);
                h.CoreShadow.color = new Color(0f, 0f, 0f,
                    Style.CoreShadowStrength * wake * (1f - open * 0.7f));
            }
            if (h.Essence == null && h.EssenceOuter == null && h.EssenceInner == null)
            {
                return;
            }
            // ---- THE PASSENGER ----
            float ignite = Ease(Span(h.SeatClock,
                Style.SeatCoreWake + Style.SeatMembraneSpread + Style.SeatRibForm,
                Style.SeatCoreWake + Style.SeatMembraneSpread + Style.SeatRibForm
                    + Style.SeatEssence));
            float reveal = 1f;
            float alphaCore = ignite;
            if (rupture >= 0f)
            {
                float revealAt = Style.RuptureFirstRib + Style.RuptureRibStagger;
                float shown = Ease(Span(rupture, revealAt, revealAt + Style.RuptureCoreReveal));
                reveal = 1f + (Style.RuptureEssenceReveal - 1f) * shown;
                float collapseAt = revealAt + Style.RuptureCoreReveal + Style.RuptureEssenceHold;
                float collapse = Ease(Span(rupture, collapseAt,
                    collapseAt + Style.RuptureEssenceCollapse));
                // It FOLDS to its middle rather than shrinking evenly.
                reveal *= 1f - collapse * 0.75f;
                alphaCore = 1f - collapse;
            }
            float size = Style.EssenceSize * cube * reveal;
            float squash = rupture >= 0f ? 1f - (1f - reveal) * 0.4f : 1f;
            var seatAt = new Vector2(at.x, at.y + cube * 0.008f);
            Color passenger = PassengerColour(h.Passenger);
            float boost = 1f + Style.IdleEssenceBoost * Mathf.Max(push, circulate)
                + 0.2f * Mathf.Max(0f, reveal - 1f);
            Color tint = new Color(
                Mathf.Clamp01(passenger.r * boost * Style.EssenceBrightness),
                Mathf.Clamp01(passenger.g * boost * Style.EssenceBrightness),
                Mathf.Clamp01(passenger.b * boost * Style.EssenceBrightness),
                Mathf.Clamp01(alphaCore));
            if (rupture >= 0f)
            {
                float toPlum = Ease(Span(rupture, Style.RuptureTotal - 0.2f,
                    Style.RuptureTotal - 0.05f));
                tint = Color.Lerp(tint, new Color(DeepPlum.r, DeepPlum.g, DeepPlum.b, tint.a),
                    toPlum);
            }
            // THREE SHELLS OF THE SAME HUE. A dark husk the joker's colour is carried inside, the
            // colour itself, and a pale centre. All three are that colour scaled - multiplying and
            // lifting keep the ratio between the channels, so a blue passenger is blue in all
            // three - because the point of this is that the player can name the joker they are
            // about to lose, and a wash toward black or white takes exactly that away.
            var husk = new Color(tint.r * Style.EssenceOuterDarken,
                tint.g * Style.EssenceOuterDarken, tint.b * Style.EssenceOuterDarken,
                tint.a * 0.92f);
            var heart = new Color(
                Mathf.Lerp(tint.r, 1f, Style.EssenceInnerLighten),
                Mathf.Lerp(tint.g, 1f, Style.EssenceInnerLighten),
                Mathf.Lerp(tint.b, 1f, Style.EssenceInnerLighten), tint.a);
            PaintEssenceShell(h.EssenceOuter, seatAt, size * Style.EssenceOuterScale, squash, husk);
            PaintEssenceShell(h.Essence, seatAt, size, squash, tint);
            PaintEssenceShell(h.EssenceInner, seatAt, size * Style.EssenceInnerScale, squash,
                heart);
        }

        /// <summary>One shell of the passenger's seed.</summary>
        private static void PaintEssenceShell(SpriteRenderer r, Vector2 at, float size,
            float squash, Color tint)
        {
            if (r == null)
            {
                return;
            }
            r.transform.localPosition = new Vector3(at.x, at.y, 0f);
            Fit(r, size * squash, size * 1.15f);
            r.color = tint;
        }

        private void PaintFlecks(HostPiece h, Vector2 at, float cube, float sever)
        {
            if (h.Flecks.Count == 0)
            {
                return;
            }
            float k = sever >= 0f
                ? Span(sever, Style.RuptureFirstRib, Style.RuptureFirstRib + Style.RuptureResidue)
                : 0f;
            float coreK = sever >= 0f
                ? Span(sever, Style.RuptureTotal - 0.28f, Style.RuptureTotal - 0.05f)
                : 0f;
            for (int i = 0; i < h.Flecks.Count; i++)
            {
                SpriteRenderer f = h.Flecks[i];
                if (f == null)
                {
                    continue;
                }
                bool isCore = i >= Style.RuptureFleckCount;
                float t = isCore ? coreK : k;
                Vector2 dir = h.FleckDirs[i];
                Vector2 p = at + dir * (Ease(t) * cube * (isCore ? 0.3f : 0.45f));
                f.transform.localPosition = new Vector3(p.x, p.y, 0f);
                float s = cube * 0.045f * (1f - t);
                Fit(f, s, s);
                Color c = isCore
                    ? Color.Lerp(PassengerColour(h.Passenger), DeepPlum, 0.4f)
                    : BodyViolet;
                c.a = 0.8f * (1f - t) * (t > 0f ? 1f : 0f);
                f.color = c;
            }
        }

        /// <summary>
        /// The bound joker's own accent colour, derived from its DEF ID. It must not come from the
        /// host cube's material - that says nothing about who is riding - and it must not be the
        /// parasite's plum either, which would read as an empty socket. A passenger with no colour
        /// of its own falls back to a muted warm amber.
        /// </summary>
        public static Color PassengerColour(BoundJokerIdentity passenger)
        {
            if (!passenger.Bound || string.IsNullOrEmpty(passenger.DefId))
            {
                return UnknownPassenger;
            }
            // A stable hue per joker: the same passenger is always the same colour, and two
            // different ones are very unlikely to collide.
            int hash = 17;
            for (int i = 0; i < passenger.DefId.Length; i++)
            {
                hash = hash * 31 + passenger.DefId[i];
            }
            float hue = ((hash & 0x7FFFFFFF) % 360) / 360f;
            return Color.HSVToRGB(hue, 0.62f, 0.95f);
        }

        private void PaintHarness(SpriteRenderer r, float tone, float relief, float sink,
            float tension, float sheen, float severAt, float alpha)
        {
            PaintHarness(r, tone, relief, sink, tension, sheen, severAt, alpha,
                ParasiteContrastProfile.For(Color.white));
        }

        /// <summary>Paints one piece in a HOST'S OWN palette. Everything but the colours is the
        /// same; the colours are what keep the organism visible on a near-black block.</summary>
        private void PaintHarness(SpriteRenderer r, float tone, float relief, float sink,
            float tension, float sheen, float severAt, float alpha, ParasiteContrast c)
        {
            PaintHarness(r, tone, relief, sink, tension, sheen, severAt, alpha, c, 0f);
        }

        /// <summary>The same, with the dial only the nest wants: its GROWTH RIDGES.</summary>
        private void PaintHarness(SpriteRenderer r, float tone, float relief, float sink,
            float tension, float sheen, float severAt, float alpha, ParasiteContrast c,
            float ridges)
        {
            if (r == null)
            {
                return;
            }
            r.color = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha));
            if (HarnessMaterial() == null)
            {
                // No shader: flat plum in this host's own weight, which still says something is
                // wrapped round the cube.
                r.color = new Color(c.Skin.r, c.Skin.g, c.Skin.b, Mathf.Clamp01(alpha));
                return;
            }
            block.Clear();
            block.SetColor(DeepId, c.Fold);
            block.SetColor(BodyId, c.Skin);
            block.SetColor(RoseId, c.Vein);
            // The contour light, and only as much of it as this block needs.
            block.SetFloat(RimId, c.RimStrength);
            block.SetColor(RimColourId, c.Edge);
            block.SetFloat(ToneId, tone);
            block.SetFloat(ReliefId, relief);
            block.SetFloat(SinkId, sink);
            block.SetFloat(TensionId, tension);
            block.SetFloat(SheenId, sheen);
            block.SetFloat(SeverId, severAt);
            block.SetFloat(RidgesId, ridges);
            block.SetFloat(FaceHalfId, CompressedCubeView.FaceHalfOf(r));
            r.SetPropertyBlock(block);
        }

        // =================================================================== pooling

        private readonly Stack<SpriteRenderer> spare = new Stack<SpriteRenderer>();

        private Material plainMaterial;

        private static void Fit(SpriteRenderer r, float width, float height)
        {
            if (r == null || r.sprite == null)
            {
                return;
            }
            Vector2 unit = r.sprite.bounds.size;
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
                var go = new GameObject("Parasite");
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
            r.SetPropertyBlock(null);
            spare.Push(r);
        }
    }
}
