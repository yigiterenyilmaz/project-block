// PURPOSE: "Tılsım" - SPIRIT RECLAMATION. The power harvests the ghost cubes hanging off the
// board's edge and turns the dead space they occupied into BONUS GROUND. Owned by BoardView like
// the rot, the snake, the press, the parasite and Mapus, because most of what it draws is a
// PRESENCE that outlives every repaint - and one of its beats outlives the round itself.
//
// WHAT MAKES THIS POWER DIFFERENT FROM EVERY OTHER EFFECT IN THE GAME: it is paid for in one round
// and delivered in the next, with a market screen in between. So it cannot be one animation. It is
// five, and the gap between the second and the third is a round boundary:
//
//   HARVEST   the ghosts are taken. Not the ordinary cluster burst - that is a material break, and
//             this is a SPIRITUAL one: each ghost lights from within in its OWN colour, its shell
//             comes apart into soft spectral flakes rather than debris, and what is left is a soul
//             kernel. Every ghost scores; only the ones the rules can reclaim from leave a seed.
//   CLAIM     the ground the harvest bought goes DARK, cell by cell, as vines grow out of the
//             board's edge and reach it. What is there is NOT ground - it is a curse stain, a
//             promise, and the player must not be able to mistake it for somewhere they can play
//             THIS round, because they cannot. The darkness is built from the exact reclaimed
//             cells (TalismanStain) and the plant is what carries it out over them; what fills
//             in between them is the plant's own shadow rather than more plant
//             (TalismanTendril).
//   REVEAL    next round, the board is built with the ground in it. The vines lift and retract and
//             what was underneath them all along is solid bonus floor.
//   PRESENCE  and then it just sits there for a round, which is the part the player actually looks
//             at. Four OPEN corner runes and no closed frame: an ordinary cell has a structural
//             border that a line runs through, and this one does not, because a line never waits
//             for bonus ground. The rule is legible without a word of UI.
//   RECALL    at the end of the round the gift is taken back. The ground stops being matter and is
//             drawn home along the vines. Not a collapse and not a fade.
//
// THE VIEW DECIDES NONE OF IT. TalismanActivationVisuals says which ghosts there were, what each
// one looked like BEFORE it was removed, and which of them the rules could reclaim ground from -
// that last one is the "board never grows left or down" rule and it is written in exactly one
// place. TalismanGroundVisuals says which cells the next board actually received. Neither is
// derived here, and the "which of these is reclaimable" question in particular is never asked
// twice.

using System.Collections.Generic;
using ProjectBlock.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace ProjectBlock.View
{
    public sealed class TalismanView : MonoBehaviour
    {
        /// <summary>Every number the look is made of.</summary>
        public static class Style
        {
            // ---- the palette ----
            /// <summary>Jade, and it never becomes a green anyone could mistake for a pickup. The
            /// power is spiritual, not radioactive.</summary>
            public static readonly Color VineDeep = new Color(0.07f, 0.15f, 0.14f);

            public static readonly Color VineBody = new Color(0.16f, 0.3f, 0.28f);

            public static readonly Color VineHigh = new Color(0.34f, 0.56f, 0.48f);

            /// <summary>Muted old gold - the talisman's own accent. Not yellow, not bright.
            /// </summary>
            public static readonly Color RuneDeep = new Color(0.28f, 0.22f, 0.12f);

            public static readonly Color RuneBody = new Color(0.56f, 0.46f, 0.26f);

            public static readonly Color RuneHigh = new Color(0.84f, 0.74f, 0.5f);

            /// <summary>What a thing washes toward as it stops being matter.</summary>
            public static readonly Color Spectral = new Color(0.72f, 0.86f, 0.8f);

            public static readonly Color Rim = new Color(0.5f, 0.68f, 0.6f);

            // ---- the harvest ----
            /// <summary>How long one ghost takes to light, come apart and leave its kernel - and
            /// how far apart they start, so twenty of them are a wave rather than twenty times as
            /// long as one.</summary>
            public static float HarvestCharge = 0.09f;

            public static float HarvestRupture = 0.22f;

            public static float HarvestStagger = 0.024f;

            /// <summary>However many ghosts there are, the whole harvest fits in this. Past it the
            /// stagger is squeezed rather than the animation stretched.</summary>
            public static float HarvestCap = 1.1f;

            public static int ShardCount = 6;

            public static float ShardSpread = 0.34f;

            public static float ShardSize = 0.17f;

            /// <summary>How long a shard keeps the block's OWN colour before it turns spectral.
            /// The harvest has to start from what the block was; a cyan flash from frame one
            /// throws away the only thing that makes it that block's death.</summary>
            public static float ShardKeepsColour = 0.45f;

            public static float KernelSize = 0.26f;

            /// <summary>The talisman seed a harvested ghost leaves. It has to be an OBJECT on
            /// the board - at a tenth of a cell, drawn under everything, it was a gold pixel, and
            /// a gold pixel is a UI dot.</summary>
            public static float SeedSize = 0.17f;

            // ---- the claim: ONE VINE SYSTEM PER COMPONENT ----
            /// <summary>How long the board's edge takes to answer before anything grows out of it.
            /// </summary>
            public static float EdgeWake = 0.13f;

            // ---- THE COVER: A GROWTH NETWORK, GROWN UNTIL THE AREA IS COVERED ----
            /// <summary>
            /// HOW MUCH OF THE CLAIM HAS TO END UP UNDER VINE, averaged over the whole thing.
            ///
            /// THIS NUMBER REPLACED A TABLE, and that is the whole change. Every pass before this
            /// one asked "how many sprites does a 2x2 get" - three, then six, then twenty, then
            /// three again - and every answer was wrong at some other size, because the question
            /// is wrong: one cell and nine cells do not want the same number, they want the same
            /// DENSITY. So nothing counts actors any more. The network keeps putting out branches
            /// at whatever part of the claim is barest until these are met or the cap is hit, and
            /// how many that takes is an outcome rather than a setting.
            /// </summary>
            public static float TargetCoverage = 0.76f;

            /// <summary>No single reclaimed cell may be left barer than this, however good the
            /// average is - an average is exactly how a claim ends up dense at the entry and empty
            /// at the far corner.</summary>
            public static float TargetCellCoverage = 0.46f;

            /// <summary>The claim's outer ring gets a little more than its middle, because a
            /// territory reads as TAKEN by its edge: dense in the centre with bare edges is a
            /// blob sitting on a square, not an overgrowth.</summary>
            public static float EdgeBias = 1.15f;

            /// <summary>The safety net, per component. Coverage comes first; this only stops a
            /// pathological shape from growing forever.</summary>
            public static int MaxSegments = 26;

            public static int FillPasses = 2;

            /// <summary>
            /// THE GENERATIONS: backbone, primary, secondary, fill, terminal. Each is smaller than
            /// its parent AND STOPS ON AN EARLIER FRAME, and the second half of that is the trick
            /// this system turns on.
            ///
            /// The sheet's last frame is a big spiral tangle. Sixteen segments all settling on it
            /// is the PNG pile all over again, only worse. But the sheet is a GROWTH: frame 4 is a
            /// perfectly good short curl and frame 6 a perfectly good medium one. So a small
            /// branch grows 1..5 and simply stays there. One asset, five lengths of vine.
            /// </summary>
            public static readonly float[] GenerationScale = { 0.95f, 0.74f, 0.57f, 0.44f, 0.34f };

            /// <summary>The frame each generation settles on, as an index. Backbone the last,
            /// terminal a third of the way in.</summary>
            public static readonly int[] GenerationFrame = { 8, 7, 6, 5, 4 };

            /// <summary>How long ONE frame takes, per generation. A twig fills out faster than a
            /// trunk, and that difference is most of what makes the spread read as overgrowth
            /// rather than as a queue of animations.</summary>
            public static readonly float[] GenerationFrameTime =
                { 0.068f, 0.056f, 0.046f, 0.038f, 0.032f };

            /// <summary>How far into a parent's own growth its children start. Below 1, so the
            /// network is always growing in several places at once - which is also why a big claim
            /// does not take nine times as long as a small one.</summary>
            public static float ChildSpawnAt = 0.55f;

            /// <summary>How many tips may be growing at the same moment, by claim size. Without a
            /// ceiling the branching goes 1, 2, 4, 8 and the whole thing arrives at once.
            /// </summary>
            public static int MaxTipsSmall = 3;

            public static int MaxTipsMedium = 5;

            public static int MaxTipsLarge = 8;

            /// <summary>Candidate directions tried per branch, scored against each other.</summary>
            public static int Candidates = 5;

            /// <summary>What a candidate is scored on: what it would cover, minus what it would
            /// bury of what is already there, minus how far it strays off the claim.</summary>
            public static float OverlapPenalty = 0.9f;

            public static float OutsidePenalty = 1.3f;

            /// <summary>How far a child may swing off its parent's own heading.</summary>
            public static float BranchSwing = 55f;

            public static float VineTiltSpread = 10f;

            /// <summary>
            /// WHERE THE ART ACTUALLY IS. The drawing does not sit on its own foot: at the last
            /// frame its bulk is 0.53 frame-heights from the cut end of the stem, 24 degrees above
            /// its own base line. Both measured off the sheet. Solving the foot backwards from
            /// where the bulk has to land is what puts the thick of the plant on the claim instead
            /// of a cell and a half past it.
            /// </summary>
            public static float MassAt = 0.531f;

            public static float ArtRise = 23.6f;

            /// <summary>How big one cell of the claim is in vine terms - the backbone's own size
            /// before its generation scale.</summary>
            public static float VineReach = 1.2f;

            public static float VineSizeSpread = 0.1f;

            /// <summary>How far along its parent a child is planted. Not the tip (two vines in a
            /// line) and not the base (two vines side by side).</summary>
            public static float BranchAtMin = 0.55f;

            public static float BranchAtMax = 0.9f;

            /// <summary>Past this the connector reaches the claim as a CHAIN rather than as one
            /// stretched sprite.</summary>
            public static float ConnectorSplit = 2.5f;

            /// <summary>However far it has to spread, the whole growth fits in this - the network
            /// grows in parallel, so a big claim is denser rather than slower.</summary>
            public static float ClaimCap = 1.2f;

            /// <summary>The backbone's own run, end to end - what the seed and the shroud clocks
            /// are measured against, since they follow the trunk rather than any one twig.
            /// </summary>
            public static float VineGrow = 0.55f;

            // ---- what makes it sit ON something ----
            /// <summary>The shadow a vine drops on what it is crawling over, and how far. Without
            /// it the vine is drawn on the surface rather than lying on it.</summary>
            /// <summary>
            /// TIGHT, AND FAINT. It is the same sprite in black, a pixel or two down - a shadow
            /// shaped like the thing casting it.
            ///
            /// It was three times this dark, and with a dozen segments in a claim the overlaps
            /// stacked into a black smear behind the network. A contact shadow's job is to say
            /// "this is lying on something", and that is a whisper, not a mass.
            /// </summary>
            public static float ShadowStrength = 0.14f;

            public static float ShadowOffset = 0.022f;

            /// <summary>Barely larger than the sprite. Anything more is a halo, and a halo is the
            /// giant grey disc this replaced.</summary>
            public static float ShadowSpread = 1.02f;

            public static float KnotSize = 0.1f;

            /// <summary>The root origin in the board's rim. Big enough to be a place the vine
            /// came OUT of - at a tenth of a cell it was a speck and the cover read as three
            /// ornaments floating next to the board.</summary>
            public static float OriginSize = 0.3f;

            public static readonly Color OriginRecess = new Color(0.03f, 0.055f, 0.06f);

            // ---- THE CURSE, and where its numbers actually live ----
            /// <summary>
            /// THE DARKNESS IS NOT IN THIS FILE.
            ///
            /// What lies under a claim is two systems with numbers of their own:
            /// <see cref="TalismanStain.Style"/> for the baked ground - patch, neighbour bridge,
            /// corner merge, edge bleed, and the two passes that take the colour out of it - and
            /// <see cref="TalismanTendril.Style"/> for the soft meshes: the shadow roots, the
            /// veil pockets, and the streams the darkness leaves along when the gift goes home.
            ///
            /// They are there rather than here because each set was arrived at by measuring what
            /// it does TOGETHER, and a number split from the thing it was measured against is a
            /// number nobody can check. What is left in this block is POLICY: how many of each a
            /// claim gets, and when.
            /// </summary>
            /// <summary>How far through its own growth a vine is when the cell under it goes
            /// cold. EVERYTHING the darkness does hangs off this, which is the difference between
            /// the ground going out because the plant passed over it and two animations running
            /// side by side on similar timers.</summary>
            public static float VineArriveAt = 0.55f;

            /// <summary>
            /// THE CURSE DEPTH AROUND A VINE - and it is NOT the contact shadow.
            ///
            /// The shadow is tight, offset and says the vine is lying on something. This is
            /// centred, a little wider, and says the vine took the light with it. Both, or the
            /// plant either floats above the claim or drags a smear behind it.
            /// </summary>
            public static float DepthSpread = 1.1f;

            public static float DepthStrength = 0.11f;

            public static readonly Color Depth = new Color(0.012f, 0.04f, 0.038f);

            /// <summary>How many shadow tendrils a claim gets: about three for a single cell,
            /// seven for a 2x2, a dozen for a 3x3. They are what puts the claim's empty parts
            /// under something without growing more hero art into them.</summary>
            public static float TendrilsBase = 2.4f;

            public static float TendrilsPerCell = 1.05f;

            public static int TendrilsCap = 18;

            /// <summary>And how many veil pockets. FEW, and coverage-driven: they exist to take
            /// the BIGGEST holes out of the picture, and a pocket in every gap is the solid sheet
            /// this whole system is built to avoid.</summary>
            public static float PocketsPerCell = 0.34f;

            public static int PocketsCap = 6;

            // ---- THE SEAL: old gold, and the claim ends DARKER than it began ----
            /// <summary>
            /// The talisman's own gold running out through the network once, at the end.
            ///
            /// It is a warmth carried in the vine's own colour, never a light drawn over it, and
            /// the beat it belongs to is a LOCK CLOSING rather than a firework: the region settles
            /// a shade darker and a shade tighter behind it.
            /// </summary>
            public static float SealPulse = 0.34f;

            public static float SealWarmth = 0.5f;

            public static readonly Color SealGold = new Color(0.82f, 0.68f, 0.36f);

            /// <summary>How much darker everything is once the seal has passed.</summary>
            public static float SealDarken = 0.16f;

            /// <summary>
            /// WHAT THE SEAL DOES TO THE GROUND, which is the half of it that used to be missing.
            ///
            /// The energy leaves the root origin and runs the network by the order the vines grew
            /// in - and because the stain carries that same order in a channel, the ground it has
            /// just passed over goes a few per cent darker behind it for a moment. One parameter,
            /// no second timeline. Then the whole mass tightens two or three per cent from its
            /// rim, and that contraction IS the lock closing: no circle, no flash.
            /// </summary>
            public static float SealSweep = 0.05f;

            public static float SealSweepWidth = 0.12f;

            public static float SealTighten = 0.025f;

            /// <summary>THE IDLE DARK CRAWL: the same band, at a twentieth of the strength and a
            /// fifth of the speed, once every several seconds. Never the whole area at once - a
            /// region that breathes as one is a UI element.</summary>
            public static float CrawlMinInterval = 6f;

            public static float CrawlMaxInterval = 11f;

            public static float CrawlDuration = 0.8f;

            public static float CrawlGain = 0.045f;

            /// <summary>A claim sits in the outside space, off the board, and it must not read as
            /// floor. It hovers instead: a spectral thing, lifted, with no contact shadow.
            /// </summary>
            public static float ClaimLift = 0.014f;

            // ---- the presence: bonus ground ----
            /// <summary>The four corner runes, and how far in from the cell's corner they sit.
            /// They NEVER join up: the gap between them is the whole message.</summary>
            public static float CornerSize = 0.3f;

            public static float CornerInset = 0.6f;

            public static float CornerOpacity = 0.7f;

            /// <summary>The rarest idle in the game: one corner warms, then the one across from
            /// it. No cell-wide pulse, no synchronised board blink.</summary>
            public static float IdleMinInterval = 5f;

            public static float IdleMaxInterval = 9f;

            public static float IdleWarm = 0.45f;

            public static float IdleDuration = 0.9f;

            // ---- the reveal ----
            public static float RevealLift = 0.26f;

            /// <summary>How much of the cover's RETREAT a cell's rune takes to come up behind it,
            /// in the stain's own front units rather than in seconds - which is the point: it is
            /// measured against the darkness leaving, not against a clock running beside it.
            /// </summary>
            public static float RuneWake = 0.22f;

            /// <summary>How fast the cover comes off compared with how it went on. A shade
            /// quicker than the growth - letting go is not as deliberate as taking hold - but
            /// nowhere near the third of the time it used to get.</summary>
            public static float RetractRate = 0.85f;

            public static float RevealTotalCap = 1.45f;

            // ---- the recall ----
            public static float RecallDim = 0.18f;

            public static float RecallPerCell = 0.022f;

            public static float RecallFlight = 0.34f;

            public static int FlakeCount = 4;

            public static float FlakeSize = 0.16f;

            public static float RecallTotalCap = 1f;
        }

        /// <summary>What the lab can switch off one at a time.</summary>
        public static class Layers
        {
            public static bool ShowHarvest = true;

            /// <summary>The cover itself - the sprite-sheet vines.</summary>
            public static bool ShowVines = true;

            /// <summary>The rune mark on the board's edge the cover comes out of.</summary>
            public static bool ShowKnots = true;

            /// <summary>The CONTACT shadows - what makes a vine lie ON the surface rather
            /// than be drawn on it. Tight and offset; not the curse depth below it.</summary>
            public static bool ShowContactShadows = true;

            /// <summary>
            /// THE CURSE STAIN, one switch per part it is ASSEMBLED from.
            ///
            /// These go into the BAKE rather than hiding a renderer afterwards, because there is
            /// no renderer to hide: the parts are one field, combined with max, drawn once. So
            /// "bridges only" really is a claim built from bridges - which is the only version of
            /// that test worth having.
            /// </summary>
            public static bool ShowCellPatches = true;

            public static bool ShowNeighborBridges = true;

            public static bool ShowCornerMerges = true;

            public static bool ShowEdgeBleed = true;

            /// <summary>The LOCAL COLOUR DRAIN - the multiply pass. Off, the stain is an alpha
            /// overlay and nothing else, which is the fastest way to see why alpha alone was
            /// never going to be enough on a linear-space board.</summary>
            public static bool ShowLocalColorDrain = true;

            /// <summary>The shadow roots crawling out of the vines into the rest of the claim.
            /// </summary>
            public static bool ShowShadowTendrils = true;

            /// <summary>The skins stretched over the biggest holes left between them.</summary>
            public static bool ShowVeilPockets = true;

            /// <summary>The extra darkness around a vine's own silhouette.</summary>
            public static bool ShowCurseDepth = true;

            /// <summary>The stain's ALPHA pass. It has a switch only so that "the colour drain,
            /// on its own" is a thing the lab can show - which is the one test that settles
            /// whether the multiply is pulling its weight.</summary>
            public static bool ShowStainBody = true;

            /// <summary>The gold that runs through the network when the claim locks.</summary>
            public static bool ShowSeal = true;

            public static bool ShowCorners = true;

            public static bool ShowSeeds = true;

            /// <summary>DEV ONLY. Outlines the cells the RULES actually reclaimed, and the
            /// bounding rectangle they happen to sit in - so "is the darkness following the cells
            /// or the box?" is something you look at instead of something you argue about.
            /// </summary>
            public static bool ShowActualReclaimCells = false;

            /// <summary>DEV ONLY. Paints the stain by whichever part drew each texel: patch red,
            /// bridge blue, corner merge yellow, edge tongue green.</summary>
            public static bool DebugParts = false;

            public static void AllOn()
            {
                ShowHarvest = true;
                ShowVines = true;
                ShowKnots = true;
                ShowContactShadows = true;
                ShowCellPatches = true;
                ShowNeighborBridges = true;
                ShowCornerMerges = true;
                ShowEdgeBleed = true;
                ShowLocalColorDrain = true;
                ShowShadowTendrils = true;
                ShowVeilPockets = true;
                ShowCurseDepth = true;
                ShowStainBody = true;
                ShowSeal = true;
                ShowCorners = true;
                ShowSeeds = true;
                ShowActualReclaimCells = false;
                DebugParts = false;
            }

            /// <summary>Everything the curse is made of, off - so one part can be switched back on
            /// by itself. The lab's isolation entries are this plus one line.</summary>
            public static void CurseOff()
            {
                ShowCellPatches = false;
                ShowNeighborBridges = false;
                ShowCornerMerges = false;
                ShowEdgeBleed = false;
                ShowLocalColorDrain = false;
                ShowShadowTendrils = false;
                ShowVeilPockets = false;
                ShowCurseDepth = false;
                ShowStainBody = false;
            }
        }

        // =================================================================== the vine sheet

        /// <summary>
        /// THE ART: Assets/Resources/Art/Fx/talisman_vine_sheet.png - NINE frames of one plant
        /// growing, on a 5x2 grid read left to right, top row first.
        ///
        /// Every frame is planted on the SAME FOOT: the cut end of the stem, near the left of the
        /// cell, which is where the sprite pivot is. That is the whole reason the sheet works as a
        /// growth: the transform position is where the vine is planted and its rotation turns the
        /// plant about its own base, so the base stays nailed down while everything above it
        /// reaches out. Centre the frames instead and the plant slides across the board while it
        /// grows, which reads as nine different vines being swapped.
        /// </summary>
        private const string SheetPath = "Art/Fx/talisman_vine_sheet";

        private const int FrameCount = 9;

        private const int FrameColumns = 5;

        private const int FrameRows = 2;

        /// <summary>The foot as a fraction of one frame - x from the left, y from the BOTTOM,
        /// which is what Sprite.Create wants. Measured off the packed sheet.</summary>
        private static readonly Vector2 FootPivot = new Vector2(11f / 224f, 54f / 216f);

        /// <summary>
        /// EACH FRAME'S SILHOUETTE, baked down to a 16x16 bitmask in the frame's own space, row 0
        /// at the BOTTOM so it lines up with the pivot's y.
        ///
        /// This is what lets the network ask "how much of the claim is actually under vine" - the
        /// question that replaced the how-many-sprites table. Read off the texture instead and the
        /// sheet would have to be CPU-readable, which costs a second copy of it in memory for a
        /// number that never changes; baked in, it is 288 bytes and no import setting.
        /// </summary>
        private static readonly ushort[][] FrameMask =
        {
            // frame 1
            new ushort[] { 0x0000, 0x0000, 0x0000, 0x0002, 0x0002, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000 },
            // frame 2
            new ushort[] { 0x0000, 0x0000, 0x0000, 0x0006, 0x001E, 0x0038, 0x0038, 0x0030, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000 },
            // frame 3
            new ushort[] { 0x0000, 0x0000, 0x0004, 0x001E, 0x003E, 0x007C, 0x0070, 0x0070, 0x0070, 0x00F0, 0x01E0, 0x01C0, 0x0000, 0x0000, 0x0000, 0x0000 },
            // frame 4
            new ushort[] { 0x0000, 0x0000, 0x0030, 0x00FE, 0x00FE, 0x01CC, 0x01C0, 0x01E0, 0x00E0, 0x0070, 0x0360, 0x03E0, 0x03C0, 0x0000, 0x0000, 0x0000 },
            // frame 5
            new ushort[] { 0x0000, 0x0000, 0x00F8, 0x01FE, 0x03FE, 0x0384, 0x0380, 0x07C0, 0x00F0, 0x00F0, 0x0660, 0x0EE0, 0x07C0, 0x0300, 0x0000, 0x0000 },
            // frame 6
            new ushort[] { 0x0000, 0x0000, 0x01F2, 0x03FE, 0x03BE, 0x038E, 0x0180, 0x01C0, 0x0CF0, 0x1CF0, 0x0FD0, 0x0F80, 0x0200, 0x0000, 0x0000, 0x0000 },
            // frame 7
            new ushort[] { 0x0100, 0x07C0, 0x0FF2, 0x0E7E, 0x003E, 0x003C, 0x0C38, 0x1C38, 0x19F8, 0x18F0, 0x3FE0, 0x0FE0, 0x0760, 0x0020, 0x0000, 0x0000 },
            // frame 8
            new ushort[] { 0x0100, 0x07C0, 0x0FF0, 0x0FFE, 0x01BE, 0x003C, 0x0E38, 0x1F38, 0x3738, 0x3638, 0x3870, 0x1FF0, 0x1FD0, 0x1A00, 0x0000, 0x0000 },
            // frame 9
            new ushort[] { 0x0000, 0x07C0, 0x0FE6, 0x1FFE, 0x1DFE, 0x1E3E, 0x0FB8, 0x07F0, 0x1BF0, 0x7DF0, 0x6CE0, 0x69C0, 0x3BC0, 0x3F80, 0x3F00, 0x2000 },
        };

        private const int MaskGrid = 16;

        /// <summary>One frame's width over its height. The sprite is one unit TALL at scale 1, so
        /// this is what turns a segment's size into its width.</summary>
        private const float FrameAspect = 224f / 216f;

        private static Sprite[] frames;

        private static bool framesLooked;

        private static bool sheetMissing;

        /// <summary>The nine frames, sliced once and shared by every claim.</summary>
        private static Sprite[] Frames()
        {
            if (!framesLooked)
            {
                framesLooked = true;
                var sheet = Resources.Load<Texture2D>(SheetPath);
                if (sheet == null)
                {
                    // Degrade rather than render nothing, like the flame sheet does: a stripped
                    // build loses the cover, it does not throw once per vine per frame.
                    sheetMissing = true;
                    Debug.LogWarning("[block_bonk] Talisman vine sheet missing: Resources/"
                        + SheetPath);
                    return null;
                }
                int cellW = sheet.width / FrameColumns;
                int cellH = sheet.height / FrameRows;
                frames = new Sprite[FrameCount];
                for (int i = 0; i < FrameCount; i++)
                {
                    int col = i % FrameColumns;
                    int row = i / FrameColumns;
                    // Texture space counts y from the BOTTOM while the sheet reads from the top,
                    // so the first row of frames is the last row of texels.
                    frames[i] = Sprite.Create(sheet,
                        new Rect(col * cellW, (FrameRows - 1 - row) * cellH, cellW, cellH),
                        FootPivot, cellH);
                }
            }
            return sheetMissing ? null : frames;
        }

        /// <summary>
        /// THE STACK, bottom to top, and every gap in it is load-bearing.
        ///
        /// The DRAIN goes first because it multiplies whatever is already in the frame - the
        /// backdrop, and during the unseal the bonus floor coming up underneath it. The STAIN
        /// lies on that, the soft meshes on the stain, then each vine's own darkness, then its
        /// contact shadow, then the vine. Roles are spaced by four so that every shadow is still
        /// under every body: a secondary's shadow falling on a connector's body puts the darkness
        /// on the wrong side of the plant.
        /// </summary>
        private const int SeedOrder = 3;

        private const int DrainOrder = 5;

        private const int StainOrder = 6;

        private const int TendrilOrder = 7;

        private const int PocketOrder = 8;

        private const int StreamOrder = 9;

        private const int DepthOrder = 10;

        private const int ShadowOrder = 14;

        /// <summary>The corner runes of standing bonus ground - ABOVE the stain. During the
        /// unseal both are on screen at once, and a rune drawn under the darkness that is leaving
        /// is a rune nobody sees arrive.</summary>
        private const int CornerOrder = 18;

        private const int VineOrder = 20;

        private const int KnotOrder = 26;

        private const int MarkOrder = 27;

        private const int ShardOrder = 28;

        /// <summary>One ghost being harvested: where it stood, what it looked like, and whether it
        /// leaves a seed. All four facts are Core's.</summary>
        private sealed class Harvest
        {
            public GridPos Cell;
            public Color Colour;
            public Sprite Face;
            public bool Reclaimable;
            public float Start;
            public SpriteRenderer Ghost;
            public SpriteRenderer Kernel;
            public readonly List<SpriteRenderer> Shards = new List<SpriteRenderer>();
            public readonly List<Vector2> ShardDirs = new List<Vector2>();
        }

        /// <summary>
        /// ONE VINE: a single run of the sheet, planted at a point and turned to face the piece of
        /// board it has to bury. The sheet does the growing; this holds WHERE it is planted, HOW
        /// BIG it gets, WHICH WAY it curls and WHEN it starts - because nine frames played
        /// identically across a claim is a tiled texture, and a tile is the one thing the eye
        /// always catches.
        /// </summary>
        private sealed class Vine
        {
            public SpriteRenderer Body;

            /// <summary>The same frame in black, dropped and spread - what puts the vine ON the
            /// board rather than drawn over it.</summary>
            public SpriteRenderer Shadow;

            /// <summary>The same frame again, CENTRED on the plant's own mass and a little wider:
            /// the curse depth. The shadow says the vine is lying on the ground; this says the
            /// ground under it is darker for its being there.</summary>
            public SpriteRenderer Depth;

            /// <summary>Where the stem is planted, in the board local space. The sprite pivot sits
            /// here, so this point does not move while the plant grows.</summary>
            public Vector2 Foot;

            /// <summary>Which way it reaches, in degrees.</summary>
            public float Angle;

            /// <summary>Its height in world units at the last frame.</summary>
            public float Size;

            /// <summary>Mirrored about its own stem, so the curl goes the other way.</summary>
            public bool Flip;

            public float Start;

            public float Duration;

            /// <summary>0 connector, 1 primary, 2 secondary. It decides the size, the sorting, the
            /// order it grows in, the order it lets go in, and whether the claim mask applies -
            /// which is five things that were all being decided by chance before.</summary>
            public int Role;

            /// <summary>The frame it settles on. A secondary at the last frame is as dense as the
            /// hero it hangs off, so it stops one earlier.</summary>
            public int Settle;

            /// <summary>Where its bulk lands and how wide that bulk is - what the overlap test
            /// asks about, and what the reveal sorts the cells by.</summary>
            public Vector2 Mass;

            /// <summary>The vine it grew out of. A branch is SUPPOSED to lie over its parent -
            /// that overlap is the join, and hiding the join is the whole reason the branch is
            /// planted on the parent's body at all - so the crowding test has to skip it. Left in,
            /// it rejected every branch in the system and a claim came out as a bare trunk.
            /// </summary>
            public Vine Parent;

            /// <summary>0 backbone, 1 primary, 2 secondary, 3 fill, 4 terminal. It decides the
            /// size, the frame it settles on and how fast it gets there - and a small branch
            /// settling on an EARLY frame is what lets one asset be five lengths of vine.
            /// </summary>
            public int Generation;

            /// <summary>How many branches already leave it, so the network spreads instead of
            /// piling everything onto whichever segment happens to be nearest.</summary>
            public int Children;
        }

        /// <summary>
        /// ONE CONNECTED COMPONENT of reclaimed cells, and the overgrowth that buries it. Not one
        /// wreath per cell: the vines are bigger than a cell and spill across each other, and they
        /// arrive in order of distance from the board - so four claimed cells are one thing that
        /// crawled over them, not four badges that appeared.
        /// </summary>
        private sealed class Claim
        {
            public readonly List<GridPos> Cells = new List<GridPos>();
            public readonly List<Vine> Vines = new List<Vine>();

            /// <summary>
            /// THE COVERAGE FIELD - five by five sample points inside every reclaimed cell, what
            /// is under vine so far, and how much each point is wanted (the claim's outer ring a
            /// little more than its middle). What the network grows against, and the thing that
            /// replaced counting sprites. Presentation only.
            /// </summary>
            public readonly List<Vector2> Field = new List<Vector2>();

            public readonly List<bool> Filled = new List<bool>();

            /// <summary>Samples nothing could reach. They stop the growth loop asking the same
            /// impossible question forever, and they are NOT counted as covered - marking them
            /// filled instead let a claim report 94% while the picture was nearly empty, which is
            /// the worst kind of bug a measurement can have: it hid the thing it was measuring.
            /// </summary>
            public readonly List<bool> Given = new List<bool>();

            public readonly List<float> Want = new List<float>();

            /// <summary>
            /// THE CURSE STAIN - one baked field for the whole component, and the two renderers
            /// that put it on the screen: the DRAIN multiplies the colour out of the ground, the
            /// STAIN lays the darkness over what is left. Same sprite, same front, two blends.
            /// </summary>
            public TalismanStain Stain;

            public SpriteRenderer StainBody;

            public SpriteRenderer StainDrain;

            /// <summary>WHEN THE VINE REACHED each reclaimed cell, in this claim's own clock -
            /// one per cell, in the same order. The single thing the darkness is hung off.
            /// </summary>
            public readonly List<float> Arrival = new List<float>();

            /// <summary>The shadow roots, the skins over what they leave, and - only during the
            /// unseal - the streams the darkness goes home along.</summary>
            public readonly List<Thread> Tendrils = new List<Thread>();

            public readonly List<Pocket> Pockets = new List<Pocket>();

            public readonly List<Thread> Streams = new List<Thread>();

            /// <summary>DEV ONLY: the reclaimed cells outlined, and their bounding box.</summary>
            public readonly List<SpriteRenderer> Marks = new List<SpriteRenderer>();

            /// <summary>When the next idle crawl is due, and how far the current one has got.
            /// </summary>
            public float CrawlWait;

            public float CrawlClock = -1f;

            public readonly List<SpriteRenderer> Seeds = new List<SpriteRenderer>();
            /// <summary>The mark on the board edge the cover comes out of - the recess sunk into
            /// the rim and the gold crack lit across it.</summary>
            public SpriteRenderer EdgeRune;

            public SpriteRenderer EdgeRecess;
            public Vector2 Origin;
            public float Clock;
            /// <summary>How long the whole system takes, so the caller can wait on it.</summary>
            public float Span;
        }

        /// <summary>
        /// A SHADOW ROOT: where it leaves the network, where it is going, and the curve between.
        ///
        /// It is also what a DARK STREAM is during the unseal - the same three points with both
        /// ends of the visible stretch moving at once, so the darkness travels INTO the vine
        /// instead of fading where it stands.
        /// </summary>
        private sealed class Thread
        {
            public TalismanTendril Mesh;
            public Vector2 From;
            public Vector2 Control;
            public Vector2 To;
            public float Width;
            public float Start;
            public float Duration;

            /// <summary>0 off a vine, 1 off another tendril. A fork may not fork.</summary>
            public int Generation;
        }

        /// <summary>A skin stretched over a hole, hung on three points of the network.</summary>
        private sealed class Pocket
        {
            public TalismanTendril Mesh;
            public readonly Vector2[] Anchors = new Vector2[4];
            public int Count;
            public float Sag;
            public float Start;
        }

        /// <summary>One cell of standing bonus ground.</summary>
        private sealed class Ground
        {
            public GridPos Cell;
            public readonly SpriteRenderer[] Corners = new SpriteRenderer[4];
            public readonly List<SpriteRenderer> Flakes = new List<SpriteRenderer>();
            public int Distance;
            public float IdleWait;
            public float IdleClock = -1f;
            /// <summary>Which corner warms first this time - they alternate across the diagonal.
            /// </summary>
            public int IdleCorner;
        }

        private readonly List<Harvest> harvests = new List<Harvest>();

        private readonly List<Claim> claims = new List<Claim>();

        private readonly List<Ground> ground = new List<Ground>();

        private float harvestClock = -1f;

        private float revealClock = -1f;

        private float recallClock = -1f;

        private int lastActivation;

        private int lastGround;

        private MaterialPropertyBlock block;

        private float cellSize;

        private System.Func<GridPos, Vector2> toWorld;

        private void Awake()
        {
            block = new MaterialPropertyBlock();
        }

        private void OnDestroy()
        {
            // The pooled soft meshes each own a Mesh, and a Mesh is an asset rather than a
            // component: destroying the GameObject it hangs off leaves it behind. Everything
            // else here is a child renderer and goes with the view.
            ClearClaims();
            while (spareThreads.Count > 0)
            {
                spareThreads.Pop().Destroy();
            }
        }

        /// <summary>True while anything is playing - what the lab waits on.</summary>
        public bool Busy
        {
            get { return harvestClock >= 0f || revealClock >= 0f || recallClock >= 0f; }
        }

        /// <summary>
        /// The harvest, straight from the power's own report. Every ghost's face and colour come
        /// from the snapshot Core took BEFORE it removed them - an animation that begins from a
        /// block's own colour cannot begin from a block that has already been deleted - and which
        /// of them leaves a seed is the rules' answer, not a coordinate test repeated here.
        /// </summary>
        public void PlayHarvest(BoardView view, TalismanActivationVisuals report)
        {
            if (view == null || report == null || report.Serial == lastActivation)
            {
                return;
            }
            lastActivation = report.Serial;
            Bind(view);
            ClearHarvest();
            ClearClaims();
            if (!Layers.ShowHarvest || report.Ghosts.Count == 0)
            {
                return;
            }
            // A WAVE, NOT A QUEUE. Twenty ghosts must not take twenty times as long as one, so the
            // stagger is squeezed to fit the cap rather than the animation being stretched.
            float span = Style.HarvestCharge + Style.HarvestRupture;
            float stagger = Style.HarvestStagger;
            if (report.Ghosts.Count > 1)
            {
                float wanted = stagger * (report.Ghosts.Count - 1) + span;
                if (wanted > Style.HarvestCap)
                {
                    stagger = Mathf.Max(0.004f,
                        (Style.HarvestCap - span) / (report.Ghosts.Count - 1));
                }
            }
            for (int i = 0; i < report.Ghosts.Count; i++)
            {
                HarvestedGhost g = report.Ghosts[i];
                var h = new Harvest
                {
                    Cell = g.Cell,
                    Reclaimable = g.Reclaimable,
                    Colour = ViewUtil.CubeMaterialColor(g.Cube),
                    // The ghost's OWN face - which comes from its card's element, not its
                    // kind, so it has to be asked of the board.
                    Face = view.FaceOf(g.Cube),
                    Start = i * stagger
                };
                h.Ghost = Rent(h.Face != null ? h.Face : ViewUtil.RoundedSprite, ShardOrder, null);
                h.Kernel = Rent(TalismanShapes.Kernel, SeedOrder, SpiritMaterial());
                for (int k = 0; k < Style.ShardCount; k++)
                {
                    h.Shards.Add(Rent(TalismanShapes.Flake, ShardOrder, SpiritMaterial()));
                    float a = (k * 2.39996f) % (Mathf.PI * 2f);
                    h.ShardDirs.Add(new Vector2(Mathf.Cos(a), Mathf.Sin(a)));
                }
                harvests.Add(h);
            }
            harvestClock = 0f;

            // AND THE CLAIM. Only the cells the rules said were reclaimable get one.
            BuildClaims(view, report.Reclaimed);
            // It waits for the harvest: the seeds have to be on the ground before anything comes
            // looking for them.
            for (int i = 0; i < claims.Count; i++)
            {
                claims[i].Clock = -Style.HarvestCap * 0.55f;
            }
        }

        /// <summary>
        /// The ground as the next board actually received it. A new serial is a fresh gift and
        /// plays the unwrapping; an empty report after a full one is the gift being recalled.
        /// Called every repaint, so it must be cheap and idempotent.
        /// </summary>
        public void Sync(BoardView view, TalismanGroundVisuals report)
        {
            if (view == null)
            {
                return;
            }
            Bind(view);
            bool has = report != null && report.Any;
            if (has && report.Serial != lastGround)
            {
                lastGround = report.Serial;
                ClearClaims();
                ClearGround();
                for (int i = 0; i < report.Cells.Count; i++)
                {
                    ground.Add(BuildGround(report.Cells[i], i));
                }
                // THE REVEAL IS THE CLAIM RUNNING BACKWARDS. The cover the player is about to see
                // lifted is built by the same builder that grew it last round, so what retracts is
                // the same kind of thing that arrived - not a second, tidier vine drawn for the
                // occasion.
                BuildClaims(view, report.Cells);
                // It has been standing since last round, so it starts finished rather than
                // growing in front of the player a second time.
                for (int i = 0; i < claims.Count; i++)
                {
                    claims[i].Clock = claims[i].Span + 1f;
                    claims[i].CrawlClock = -1f;
                    BuildStreams(claims[i]);
                }
                revealClock = 0f;
                recallClock = -1f;
                HoldFloor(view, report.Cells);
            }
            else if (!has && ground.Count > 0 && recallClock < 0f)
            {
                // The board no longer carries the gift: it is being taken back.
                recallClock = 0f;
                revealClock = -1f;
                for (int i = 0; i < ground.Count; i++)
                {
                    for (int k = 0; k < Style.FlakeCount; k++)
                    {
                        ground[i].Flakes.Add(
                            Rent(TalismanShapes.Flake, ShardOrder, SpiritMaterial()));
                    }
                }
            }
        }

        public void Stop()
        {
            ClearHarvest();
            ClearClaims();
            ClearGround();
            harvestClock = -1f;
            revealClock = -1f;
            recallClock = -1f;
        }

        private void Bind(BoardView view)
        {
            owner = view;
            cellSize = view.CellWorldSize;
            toWorld = view.CellToWorld;
        }

        /// <summary>
        /// The board, kept for ONE reason: the bonus floor has to be held back while the cover is
        /// still lying on it.
        ///
        /// The next round's board is BUILT with the ground in it, so without this the gift is
        /// simply there from the first frame with some darkness fading off the top of it - and
        /// the whole beat is that the cover LIFTS and what was underneath is revealed. It is the
        /// same bargain BossMoveView and the press already make with the board.
        /// </summary>
        private BoardView owner;

        private readonly List<GridPos> held = new List<GridPos>();

        private readonly List<GridPos> releasing = new List<GridPos>();

        /// <summary>Blanks the floor the cover is about to be pulled off.</summary>
        private void HoldFloor(BoardView view, IReadOnlyList<GridPos> cells)
        {
            ReleaseFloor();
            if (view == null || cells == null)
            {
                return;
            }
            for (int i = 0; i < cells.Count; i++)
            {
                held.Add(cells[i]);
            }
            view.HoldCells(held);
        }

        private void ReleaseFloor()
        {
            if (held.Count == 0)
            {
                return;
            }
            if (owner != null)
            {
                owner.ReleaseCells(held);
            }
            held.Clear();
        }

        /// <summary>
        /// Gives each cell's floor back at the moment the darkness over that cell has let go -
        /// never on a timer of its own.
        ///
        /// It asks the stain's own growth channel where the front has got to, which is the same
        /// number the shader is drawing with, so the floor cannot appear from under a patch that
        /// is still there or lag behind one that has gone.
        /// </summary>
        private void ReleaseUncovered()
        {
            if (held.Count == 0 || owner == null)
            {
                return;
            }
            float retract = revealClock < 0f ? 1f : Ease(Span(revealClock, 0f, RevealSpan()));
            releasing.Clear();
            for (int i = held.Count - 1; i >= 0; i--)
            {
                if (!Uncovered(held[i], retract))
                {
                    continue;
                }
                releasing.Add(held[i]);
                held.RemoveAt(i);
            }
            if (releasing.Count > 0)
            {
                owner.ReleaseCells(releasing);
            }
        }

        /// <summary>
        /// HOW FAR THE COVER OVER ONE CELL HAS LET GO, 0 to 1.
        ///
        /// The continuous twin of <see cref="Uncovered"/>, and it reads the same front the shader
        /// is drawing with - so the floor coming back, the rune lighting on it and the darkness
        /// leaving are three faces of one number rather than three timers that agree today.
        /// </summary>
        private float Uncover(GridPos cell)
        {
            if (revealClock < 0f)
            {
                return 1f;
            }
            float front = Mathf.Lerp(1.1f, -0.15f,
                Ease(Span(revealClock, 0f, RevealSpan())));
            for (int i = 0; i < claims.Count; i++)
            {
                Claim c = claims[i];
                for (int k = 0; k < c.Cells.Count && k < c.Arrival.Count; k++)
                {
                    if (c.Cells[k].X != cell.X || c.Cells[k].Y != cell.Y || c.Stain == null)
                    {
                        continue;
                    }
                    float when = (c.Arrival[k] + TalismanStain.Style.PatchDelay)
                        / Mathf.Max(c.Stain.Scale, 1e-4f);
                    return Mathf.Clamp01((when - front) / Style.RuneWake);
                }
            }
            return 1f;
        }

        private bool Uncovered(GridPos cell, float retract)
        {
            for (int i = 0; i < claims.Count; i++)
            {
                Claim c = claims[i];
                for (int k = 0; k < c.Cells.Count && k < c.Arrival.Count; k++)
                {
                    if (c.Cells[k].X != cell.X || c.Cells[k].Y != cell.Y)
                    {
                        continue;
                    }
                    if (c.Stain == null)
                    {
                        return true;
                    }
                    // The front the stain is actually at, against the moment this cell's own
                    // patch opened. Past it, the middle of that patch has gone.
                    float front = Mathf.Lerp(1.1f, -0.15f, Ease(retract));
                    float when = (c.Arrival[k] + TalismanStain.Style.PatchDelay)
                        / Mathf.Max(c.Stain.Scale, 1e-4f);
                    return front <= when;
                }
            }
            // Nothing is covering it, so there is nothing to lift.
            return true;
        }

        // =================================================================== building

        /// <summary>
        /// Groups the reclaimed cells into 4-connected components and grows ONE vine system into
        /// each. The grouping is presentation only - the rules hand over a flat set, and how it is
        /// drawn is this file's business - but it is what stops a scattered claim from becoming a
        /// scattered set of decorations.
        /// </summary>
        private void BuildClaims(BoardView view, IReadOnlyList<GridPos> cells)
        {
            var left = new List<GridPos>(cells);
            while (left.Count > 0)
            {
                var group = new List<GridPos> { left[0] };
                left.RemoveAt(0);
                for (int i = 0; i < group.Count; i++)
                {
                    for (int k = left.Count - 1; k >= 0; k--)
                    {
                        int dx = Mathf.Abs(left[k].X - group[i].X);
                        int dy = Mathf.Abs(left[k].Y - group[i].Y);
                        if (dx + dy == 1)
                        {
                            group.Add(left[k]);
                            left.RemoveAt(k);
                        }
                    }
                }
                claims.Add(BuildClaim(view, group));
            }
        }

        private Claim BuildClaim(BoardView view, List<GridPos> group)
        {
            var c = new Claim();
            c.Cells.AddRange(group);

            // WHERE IT COMES OUT OF THE BOARD. One origin per component, on the board edge nearest
            // its closest cell - not one per cell, which is what made every cell its own island.
            // Nothing grows out of this point any more, but it is still what the cover spreads
            // AWAY from, and it is what every vine on the component aims by.
            GameBoard board = view.Board;
            int lastX = board.MinX + board.Width - 1;
            int lastY = board.MinY + board.Height - 1;
            GridPos nearest = group[0];
            float best = float.MaxValue;
            for (int i = 0; i < group.Count; i++)
            {
                float d = Mathf.Min(Mathf.Abs(group[i].X - lastX), Mathf.Abs(group[i].Y - lastY));
                if (d < best)
                {
                    best = d;
                    nearest = group[i];
                }
            }
            var edgeCell = new GridPos(Mathf.Min(nearest.X, lastX), Mathf.Min(nearest.Y, lastY));
            c.Origin = toWorld(edgeCell);
            if (Layers.ShowKnots)
            {
                // THE ROOT ORIGIN - a mark in the board's own rim that the vine comes out of, and
                // the thing whose absence made the cover read as three ornaments floating beside
                // the board. Two pieces: a recess sunk into the edge, and the talisman's crack lit
                // across it. Not a portal, not a ring, not a glow - a small fantastical split.
                // Plain too, and for the same reason: a lit recess is a bump.
                c.EdgeRecess = Rent(TalismanShapes.Recess, StainOrder, null);
                c.EdgeRune = Rent(TalismanShapes.Knot, KnotOrder, SpiritMaterial());
            }

            // WHICH WAY THE COVER COMES IN, SNAPPED TO AN AXIS. A cover that arrives at 37
            // degrees reads as a spill; one that comes straight in off the board's edge reads as
            // the board putting something out.
            Vector2 centre = Vector2.zero;
            for (int i = 0; i < group.Count; i++)
            {
                centre += toWorld(group[i]);
            }
            centre /= group.Count;
            Vector2 away = centre - c.Origin;
            Vector2 o = Mathf.Abs(away.x) >= Mathf.Abs(away.y)
                ? new Vector2(away.x >= 0f ? 1f : -1f, 0f)
                : new Vector2(0f, away.y >= 0f ? 1f : -1f);

            // THE COVERAGE FIELD: five by five sample points in every reclaimed cell, and nothing
            // else. Taking them off the board's backing rectangle instead is how a single cell
            // past the right edge would grow a whole column of vine.
            BuildField(c, group);

            // THE TARGET IS AN ACTUAL RECLAIMED CELL - the one nearest the board - never the
            // bounding box's middle, which on an L or a sparse claim is a cell nobody reclaimed.
            GridPos firstCell = group[0];
            float near = float.MaxValue;
            for (int i = 0; i < group.Count; i++)
            {
                float d2 = (toWorld(group[i]) - c.Origin).sqrMagnitude;
                if (d2 < near)
                {
                    near = d2;
                    firstCell = group[i];
                }
            }
            Vector2 entry = toWorld(firstCell);
            float run = Vector2.Distance(c.Origin, entry);

            // ================================================== THE BACKBONE
            // Planted in the crack itself, so its base never moves for the whole eight frames and
            // the vine is unmistakably coming OUT of the board.
            Vine trunk = Grow(c, nearest, c.Origin, entry, 0, 0, Style.EdgeWake, null);
            if (run > Style.ConnectorSplit * cellSize)
            {
                // A LONG RUN IS A CHAIN, NOT A STRETCH. The second length starts at the first's
                // own tip and keeps going, which is a vine still growing rather than a sprite
                // pulled over five cells.
                trunk.Settle = Style.GenerationFrame[0] - 2;
                trunk = Grow(c, nearest, Tip(trunk), entry, 1, 0,
                    trunk.Start + trunk.Duration * Style.ChildSpawnAt, trunk);
            }

            // ================================================== AND THEN IT KEEPS GOING UNTIL
            // THE CLAIM IS COVERED. No table, no per-cell loop: look at what is barest, find the
            // branch best placed to reach it, try a few headings and take the one that covers most
            // without burying what is already there.
            int maxTips = group.Count <= 2 ? Style.MaxTipsSmall
                : group.Count <= 6 ? Style.MaxTipsMedium
                : Style.MaxTipsLarge;
            int seed = 0;
            for (int pass = 0; pass < Style.FillPasses + 1; pass++)
            {
                while (c.Vines.Count < Style.MaxSegments && !Covered(c))
                {
                    Vector2 want;
                    if (!Barest(c, out want))
                    {
                        break;
                    }
                    Vine parent = BestParent(c, want, maxTips);
                    if (parent == null)
                    {
                        break;
                    }
                    Vine child = BestChild(c, nearest, parent, want, ref seed);
                    if (child == null)
                    {
                        // Nothing reached it without burying something. Mark it satisfied so the
                        // loop moves on rather than asking the same impossible question forever.
                        Forgive(c, want);
                        continue;
                    }
                    c.Vines.Add(child);
                    Absorb(c, child);
                }
            }

            float last = 0f;
            for (int i = 0; i < c.Vines.Count; i++)
            {
                last = Mathf.Max(last, c.Vines[i].Start + c.Vines[i].Duration);
            }
            if (last > Style.ClaimCap && last > 0f)
            {
                float k = Style.ClaimCap / last;
                for (int i = 0; i < c.Vines.Count; i++)
                {
                    c.Vines[i].Start *= k;
                    c.Vines[i].Duration *= k;
                }
                last = Style.ClaimCap;
            }
            c.Span = Mathf.Max(last, Style.EdgeWake + 0.2f);

            // AND THEN THE GROUND GOES OUT UNDER IT. Every one of these hangs off when the
            // VINE got somewhere, so the order is not decoration: measure the network, bake the
            // stain against it, then hang the soft layers on the network and on the holes it
            // left. Nothing here asks the rules anything - which cells were reclaimed was
            // answered once, before any of this existed.
            MeasureArrival(c, group);
            BakeStain(c, group);
            if (c.Stain != null)
            {
                // The stain finishes AFTER the last vine - the tongues are still creeping out
                // over the rim when the plant has stopped - so the seal has to wait for it.
                c.Span = Mathf.Max(c.Span, c.Stain.Scale);
            }
            if (Layers.ShowShadowTendrils)
            {
                BuildTendrils(c, group);
            }
            if (Layers.ShowVeilPockets)
            {
                BuildPockets(c, group);
            }
            if (Layers.ShowActualReclaimCells)
            {
                BuildMarks(c, group);
            }
            c.CrawlWait = Style.CrawlMinInterval
                + Random01(group[0], 5) * (Style.CrawlMaxInterval - Style.CrawlMinInterval);
            for (int i = 0; i < group.Count; i++)
            {
                if (Layers.ShowSeeds)
                {
                    c.Seeds.Add(Rent(TalismanShapes.Seed, SeedOrder, SpiritMaterial()));
                }
            }
            return c;
        }

        /// <summary>
        /// WHEN THE VINE REACHED EACH CELL, in the claim's own clock - asked of the network that
        /// was just grown, never of a distance and never of an index.
        ///
        /// A cell nothing reached still goes dark, and that is deliberate: the curse is on the
        /// whole CLAIM, not on the part the art happened to cover. It simply goes last, in the
        /// order the cells arrived in - which is distance from the board - so the darkness still
        /// visibly crawls out of the rim rather than appearing all over.
        /// </summary>
        private void MeasureArrival(Claim c, List<GridPos> group)
        {
            c.Arrival.Clear();
            for (int i = 0; i < group.Count; i++)
            {
                float best = float.MaxValue;
                for (int v = 0; v < c.Vines.Count; v++)
                {
                    Vine vine = c.Vines[v];
                    float when = vine.Start + vine.Duration * Style.VineArriveAt;
                    if (when >= best)
                    {
                        continue;
                    }
                    for (int k = i * 25; k < i * 25 + 25 && k < c.Field.Count; k++)
                    {
                        if (Hits(vine, c.Field[k]))
                        {
                            best = when;
                            break;
                        }
                    }
                }
                if (best >= float.MaxValue)
                {
                    best = Style.EdgeWake
                        + (group.Count > 1 ? i / (float)(group.Count - 1) : 0f)
                            * Mathf.Max(0.1f, c.Span - Style.EdgeWake);
                }
                c.Arrival.Add(best);
            }
        }

        /// <summary>
        /// THE CURSE STAIN, baked from the exact cells and hung off those arrivals.
        ///
        /// The layer switches go into the BAKE, not into a renderer that gets hidden afterwards,
        /// because there is only one renderer: a claim with its bridges off is genuinely a claim
        /// built without bridges, which is the only version of that test worth having.
        /// </summary>
        private void BakeStain(Claim c, List<GridPos> group)
        {
            var show = new TalismanStain.Parts
            {
                Patches = Layers.ShowCellPatches,
                Bridges = Layers.ShowNeighborBridges,
                Merges = Layers.ShowCornerMerges,
                Bleed = Layers.ShowEdgeBleed
            };
            if (!show.Patches && !show.Bridges && !show.Merges && !show.Bleed)
            {
                return;
            }
            bool packed = StainMaterial() != null;
            c.Stain = TalismanStain.Bake(group, c.Arrival, cellSize, toWorld, show, packed);
            if (c.Stain == null)
            {
                return;
            }
            if (Layers.ShowLocalColorDrain)
            {
                c.StainDrain = Rent(c.Stain.Art, DrainOrder, DrainMaterial());
            }
            if (Layers.ShowStainBody)
            {
                c.StainBody = Rent(c.Stain.Art, StainOrder, StainMaterial());
            }
        }

        /// <summary>
        /// THE SHADOW ROOTS.
        ///
        /// They go where the ART DID NOT, which is why they are built from the same coverage
        /// field the vines grew against: each one leaves the network at the branch nearest the
        /// barest place left in the claim and crawls into it. That is the whole reason this layer
        /// exists - the honest way to cover more ground with a nine-frame drawing is more of the
        /// drawing, and that pass ended as a ball of snakes. A shadow is allowed to be a plain
        /// dark curve, because nobody reads detail into a shadow.
        ///
        /// One fork each at most, and a fork may not fork: two children is a root, three is a
        /// fern, and a fern is the art competing with the art.
        /// </summary>
        private void BuildTendrils(Claim c, List<GridPos> group)
        {
            if (c.Vines.Count == 0)
            {
                return;
            }
            int want = Mathf.Clamp(
                Mathf.RoundToInt(Style.TendrilsBase + Style.TendrilsPerCell * group.Count),
                2, Style.TendrilsCap);
            int forks = 0;
            int guard = 0;
            while (c.Tendrils.Count < want && guard++ < want * 3)
            {
                Vector2 target;
                if (!Barest(c, out target))
                {
                    break;
                }
                Vine host = NearestVine(c, target);
                if (host == null)
                {
                    break;
                }
                Thread t = MakeThread(c, group[0], host.Mass, target,
                    host.Start + host.Duration * Style.VineArriveAt + 0.03f, 0,
                    c.Tendrils.Count);
                c.Tendrils.Add(t);
                Smother(c, t, cellSize * 0.22f);
                if (forks >= TalismanTendril.Style.TendrilForkMax || c.Tendrils.Count >= want
                    || Random01(group[0], 600 + c.Tendrils.Count)
                        > TalismanTendril.Style.TendrilForkChance)
                {
                    continue;
                }
                Vector2 child;
                if (!Barest(c, out child))
                {
                    continue;
                }
                Vector2 at = TalismanTendril.On(t.From, t.Control, t.To,
                    TalismanTendril.Style.TendrilForkAt);
                Thread f = MakeThread(c, group[0], at, child,
                    t.Start + t.Duration * TalismanTendril.Style.TendrilForkAt, 1,
                    c.Tendrils.Count);
                c.Tendrils.Add(f);
                Smother(c, f, cellSize * 0.18f);
                forks++;
            }
        }

        private Thread MakeThread(Claim c, GridPos salt, Vector2 from, Vector2 to, float start,
            int generation, int seed)
        {
            Vector2 d = to - from;
            var side = new Vector2(-d.y, d.x);
            float bow = (Random01(salt, 700 + seed) - 0.5f) * 2f
                * TalismanTendril.Style.TendrilBow;
            return new Thread
            {
                From = from,
                To = to,
                // A straight dark line between two points is a wire. The bow is what makes it a
                // thing that grew there.
                Control = (from + to) * 0.5f + side * bow,
                Width = cellSize * TalismanTendril.Style.TendrilWidth
                    * (generation == 0 ? 1f : TalismanTendril.Style.TendrilForkScale),
                Start = start,
                Duration = TalismanTendril.Style.TendrilGrow,
                Generation = generation,
                Mesh = RentThread(TendrilOrder)
            };
        }

        /// <summary>Marks the coverage field under a thread as taken, so the next one goes
        /// somewhere else and the pockets end up over what is genuinely still bare.</summary>
        private static void Smother(Claim c, Thread t, float radius)
        {
            float r2 = radius * radius;
            for (int k = 0; k <= 6; k++)
            {
                Vector2 p = TalismanTendril.On(t.From, t.Control, t.To, k / 6f);
                for (int i = 0; i < c.Field.Count; i++)
                {
                    if (!c.Filled[i] && (c.Field[i] - p).sqrMagnitude < r2)
                    {
                        c.Filled[i] = true;
                    }
                }
            }
        }

        private static Vine NearestVine(Claim c, Vector2 to)
        {
            Vine best = null;
            float near = float.MaxValue;
            for (int i = 0; i < c.Vines.Count; i++)
            {
                float d = (c.Vines[i].Mass - to).sqrMagnitude;
                if (d < near)
                {
                    near = d;
                    best = c.Vines[i];
                }
            }
            return best;
        }

        /// <summary>
        /// THE VEIL POCKETS - skins over the biggest holes the network still leaves.
        ///
        /// COVERAGE-DRIVEN AND FEW. They are hung on three points of the network round the barest
        /// place left, and their size is CLAMPED at both ends: under VeilMin a pocket is a smudge
        /// on one branch, over VeilMax it is a sheet across the claim, and a sheet is the blob
        /// this whole system exists to avoid.
        /// </summary>
        private void BuildPockets(Claim c, List<GridPos> group)
        {
            var anchors = new List<Vector2>();
            for (int i = 0; i < c.Vines.Count; i++)
            {
                anchors.Add(c.Vines[i].Mass);
            }
            for (int i = 0; i < c.Tendrils.Count; i++)
            {
                anchors.Add(c.Tendrils[i].To);
                anchors.Add(TalismanTendril.On(c.Tendrils[i].From, c.Tendrils[i].Control,
                    c.Tendrils[i].To, 0.6f));
            }
            if (anchors.Count < 3)
            {
                return;
            }
            int want = Mathf.Clamp(Mathf.RoundToInt(Style.PocketsPerCell * group.Count),
                1, Style.PocketsCap);
            int guard = 0;
            while (c.Pockets.Count < want && guard++ < want * 3)
            {
                Vector2 hole;
                if (!Barest(c, out hole))
                {
                    break;
                }
                var p = new Pocket { Sag = TalismanTendril.Style.VeilSag };
                float apart = cellSize * 0.3f;
                for (int take = 0; take < 3; take++)
                {
                    int best = -1;
                    float near = float.MaxValue;
                    for (int i = 0; i < anchors.Count; i++)
                    {
                        float d = (anchors[i] - hole).sqrMagnitude;
                        if (d >= near)
                        {
                            continue;
                        }
                        bool clear = true;
                        for (int k = 0; k < p.Count && clear; k++)
                        {
                            clear = (anchors[i] - p.Anchors[k]).sqrMagnitude > apart * apart;
                        }
                        if (clear)
                        {
                            near = d;
                            best = i;
                        }
                    }
                    if (best < 0)
                    {
                        break;
                    }
                    p.Anchors[p.Count++] = anchors[best];
                }
                if (p.Count < 3)
                {
                    break;
                }
                Vector2 middle = Vector2.zero;
                for (int i = 0; i < p.Count; i++)
                {
                    middle += p.Anchors[i];
                }
                middle /= p.Count;
                float reach = 0f;
                for (int i = 0; i < p.Count; i++)
                {
                    reach = Mathf.Max(reach, (p.Anchors[i] - middle).magnitude);
                }
                float wanted = Mathf.Clamp(reach,
                    cellSize * TalismanTendril.Style.VeilMin * 0.5f,
                    cellSize * TalismanTendril.Style.VeilMax * 0.5f);
                float k2 = wanted / Mathf.Max(reach, 1e-4f);
                for (int i = 0; i < p.Count; i++)
                {
                    p.Anchors[i] = middle + (p.Anchors[i] - middle) * k2;
                }
                p.Start = ArrivalNear(c, middle) + 0.1f;
                p.Mesh = RentThread(PocketOrder);
                c.Pockets.Add(p);
                for (int i = 0; i < c.Field.Count; i++)
                {
                    if (!c.Filled[i] && (c.Field[i] - middle).sqrMagnitude < wanted * wanted)
                    {
                        c.Filled[i] = true;
                    }
                }
            }
        }

        /// <summary>The arrival of the reclaimed cell nearest a point - what hangs anything that
        /// is not itself a cell off the same clock the cells run on.</summary>
        private float ArrivalNear(Claim c, Vector2 p)
        {
            float when = 0f;
            float near = float.MaxValue;
            for (int i = 0; i < c.Cells.Count && i < c.Arrival.Count; i++)
            {
                float d = (toWorld(c.Cells[i]) - p).sqrMagnitude;
                if (d < near)
                {
                    near = d;
                    when = c.Arrival[i];
                }
            }
            return when;
        }

        /// <summary>
        /// THE DARK STREAMS, and they exist only while the gift is going home.
        ///
        /// Two to four per cell, from inside the patch into the vine that is pulling it back.
        /// This is what makes the unseal a SUCTION rather than a dissolve: darkness that only
        /// fades where it stands was never really there, and the whole power rests on it having
        /// been.
        /// </summary>
        private void BuildStreams(Claim c)
        {
            if (c.Vines.Count == 0)
            {
                return;
            }
            for (int i = 0; i < c.Cells.Count; i++)
            {
                Vector2 at = toWorld(c.Cells[i]);
                int n = TalismanTendril.Style.StreamMin
                    + (int)(Random01(c.Cells[i], 51)
                        * (TalismanTendril.Style.StreamMax - TalismanTendril.Style.StreamMin + 1));
                n = Mathf.Clamp(n, TalismanTendril.Style.StreamMin,
                    TalismanTendril.Style.StreamMax);
                for (int k = 0; k < n; k++)
                {
                    float a = (Random01(c.Cells[i], 60 + k) + k / (float)n) * 6.2832f;
                    Vector2 from = at + new Vector2(Mathf.Cos(a), Mathf.Sin(a))
                        * (cellSize * 0.34f);
                    Vine host = NearestVine(c, from);
                    Vector2 to = host != null ? host.Mass : c.Origin;
                    Thread t = MakeThread(c, c.Cells[i], from, to, 0f, 0, 70 + i * 7 + k);
                    t.Width = cellSize * TalismanTendril.Style.StreamWidth;
                    t.Mesh.Order(StreamOrder);
                    // IN THE ORDER THE COVER LEAVES, which is the order it arrived, backwards.
                    t.Start = Mathf.Max(0f, c.Span - (i < c.Arrival.Count ? c.Arrival[i] : 0f));
                    t.Duration = 0.26f;
                    c.Streams.Add(t);
                }
            }
        }

        /// <summary>DEV ONLY: every reclaimed cell outlined, and the bounding rectangle they
        /// happen to sit in. The rectangle is the point - it is there to be compared against, and
        /// a claim's darkness that follows IT rather than the cells is the failure this whole
        /// system was built to end.</summary>
        private void BuildMarks(Claim c, List<GridPos> group)
        {
            for (int i = 0; i <= group.Count; i++)
            {
                c.Marks.Add(Rent(TalismanShapes.CellFrame, MarkOrder, null));
            }
        }

        // =================================================================== the coverage field

        /// <summary>Five by five sample points per reclaimed cell, each with how much of it is
        /// under vine and how much it is WANTED to be - the outer ring a little more, because a
        /// territory reads as taken by its edge.</summary>
        private void BuildField(Claim c, List<GridPos> group)
        {
            c.Field.Clear();
            c.Filled.Clear();
            c.Given.Clear();
            c.Want.Clear();
            const int n = 5;
            for (int i = 0; i < group.Count; i++)
            {
                Vector2 at = toWorld(group[i]);
                bool edgeCell = false;
                for (int k = 0; k < 4 && !edgeCell; k++)
                {
                    var step = new GridPos(group[i].X + (k == 0 ? 1 : k == 1 ? -1 : 0),
                        group[i].Y + (k == 2 ? 1 : k == 3 ? -1 : 0));
                    edgeCell = !group.Contains(step);
                }
                for (int y = 0; y < n; y++)
                {
                    for (int x = 0; x < n; x++)
                    {
                        c.Field.Add(at + new Vector2((x + 0.5f) / n - 0.5f, (y + 0.5f) / n - 0.5f)
                            * cellSize);
                        c.Filled.Add(false);
                        c.Given.Add(false);
                        c.Want.Add(edgeCell ? Style.EdgeBias : 1f);
                    }
                }
            }
        }

        /// <summary>Is a world point under this segment? The frame's own 16x16 silhouette, asked
        /// backwards - which is why the coverage is the ART's rather than a bounding box's.
        /// </summary>
        private static bool Hits(Vine v, Vector2 p)
        {
            Vector2 d = p - v.Foot;
            float a = -v.Angle * Mathf.Deg2Rad;
            float ca = Mathf.Cos(a), sa = Mathf.Sin(a);
            float lx = d.x * ca - d.y * sa;
            float ly = d.x * sa + d.y * ca;
            if (v.Flip)
            {
                ly = -ly;
            }
            float w = v.Size * FrameAspect;
            float u = lx / Mathf.Max(w, 1e-4f) + FootPivot.x;
            float t = ly / Mathf.Max(v.Size, 1e-4f) + FootPivot.y;
            if (u < 0f || u >= 1f || t < 0f || t >= 1f)
            {
                return false;
            }
            int gx = Mathf.Clamp((int)(u * MaskGrid), 0, MaskGrid - 1);
            int gy = Mathf.Clamp((int)(t * MaskGrid), 0, MaskGrid - 1);
            int f = Mathf.Clamp(v.Settle, 0, FrameMask.Length - 1);
            return (FrameMask[f][gy] & (1 << gx)) != 0;
        }

        private static void Absorb(Claim c, Vine v)
        {
            for (int i = 0; i < c.Field.Count; i++)
            {
                if (!c.Filled[i] && Hits(v, c.Field[i]))
                {
                    c.Filled[i] = true;
                }
            }
        }

        private static bool Covered(Claim c)
        {
            if (c.Field.Count == 0)
            {
                return true;
            }
            float got = 0f, want = 0f;
            for (int i = 0; i < c.Field.Count; i++)
            {
                if (c.Given[i])
                {
                    continue;
                }
                want += c.Want[i];
                if (c.Filled[i])
                {
                    got += c.Want[i];
                }
            }
            if (got / Mathf.Max(want, 1e-4f) < Style.TargetCoverage)
            {
                return false;
            }
            // AND NO CELL LEFT BEHIND. An average is exactly how a claim ends up dense at the
            // entry and bare at the far corner.
            for (int cell = 0; cell * 25 < c.Field.Count; cell++)
            {
                int hit = 0, live = 0;
                for (int i = cell * 25; i < cell * 25 + 25 && i < c.Field.Count; i++)
                {
                    if (c.Given[i])
                    {
                        continue;
                    }
                    live++;
                    if (c.Filled[i])
                    {
                        hit++;
                    }
                }
                if (live > 0 && hit / (float)live < Style.TargetCellCoverage)
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>The barest place in the claim - where the next branch is needed.</summary>
        private static bool Barest(Claim c, out Vector2 where)
        {
            where = Vector2.zero;
            int worstCell = -1;
            float worst = 1f;
            for (int cell = 0; cell * 25 < c.Field.Count; cell++)
            {
                int hit = 0, live = 0;
                for (int i = cell * 25; i < cell * 25 + 25 && i < c.Field.Count; i++)
                {
                    if (c.Given[i])
                    {
                        continue;
                    }
                    live++;
                    if (c.Filled[i])
                    {
                        hit++;
                    }
                }
                if (live == 0)
                {
                    continue;
                }
                float f = hit / (float)live;
                if (f < worst)
                {
                    worst = f;
                    worstCell = cell;
                }
            }
            if (worstCell < 0)
            {
                return false;
            }
            // The centre of gravity of what is still bare in that cell, so the branch aims at the
            // hole rather than at the cell.
            Vector2 sum = Vector2.zero;
            int n = 0;
            for (int i = worstCell * 25; i < worstCell * 25 + 25 && i < c.Field.Count; i++)
            {
                if (!c.Filled[i] && !c.Given[i])
                {
                    sum += c.Field[i];
                    n++;
                }
            }
            if (n == 0)
            {
                return false;
            }
            where = sum / n;
            return true;
        }

        /// <summary>Gives up on a spot nothing can reach, so the loop moves on.</summary>
        private static void Forgive(Claim c, Vector2 where)
        {
            int best = -1;
            float near = float.MaxValue;
            for (int i = 0; i < c.Field.Count; i++)
            {
                float d = (c.Field[i] - where).sqrMagnitude;
                if (!c.Filled[i] && !c.Given[i] && d < near)
                {
                    near = d;
                    best = i;
                }
            }
            if (best >= 0)
            {
                c.Given[best] = true;
            }
        }

        // =================================================================== growing the graph

        private void Discard(Vine v)
        {
            Return(v.Body);
            Return(v.Shadow);
            Return(v.Depth);
            v.Body = null;
            v.Shadow = null;
            v.Depth = null;
            if (v.Parent != null)
            {
                v.Parent.Children--;
            }
        }

        private void Dress(Vine v, int role)
        {
            if (!Layers.ShowVines)
            {
                return;
            }
            if (Layers.ShowCurseDepth)
            {
                v.Depth = Rent(null, DepthOrder + role, null);
            }
            if (Layers.ShowContactShadows)
            {
                v.Shadow = Rent(null, ShadowOrder + role, null);
            }
            v.Body = Rent(null, VineOrder + role, null);
            // NOTHING IS CLIPPED TO THE CLAIM ANY MORE. The branches used to be stencilled to the
            // membrane, which was a field over the whole bounding box and had room to spare; the
            // stain follows the CELLS and stops a sixth of a cell past them, so the same stencil
            // would cut the hero art off mid-stem with a hard edge. A branch overhanging the
            // darkness is the lesser problem by a wide margin - and the tendrils and the pockets
            // are what fill the claim now, not a wider net of vines.
        }

        /// <summary>Where a segment's growing END is - the far side of its bulk, which is where a
        /// chain continues from.</summary>
        private static Vector2 Tip(Vine v)
        {
            return Vector2.LerpUnclamped(v.Foot, v.Mass, 1.45f);
        }

        /// <summary>The branch best placed to reach a bare spot: near it, not too deep in the
        /// tree, and not already crowded with children.</summary>
        private static Vine BestParent(Claim c, Vector2 want, int maxTips)
        {
            Vine best = null;
            float bestScore = float.MaxValue;
            for (int i = 0; i < c.Vines.Count; i++)
            {
                Vine v = c.Vines[i];
                // A segment at the last generation can still CONTINUE a chain, so being deep is
                // no longer a reason to refuse it - only being out of children is.
                if (v.Children >= 3)
                {
                    continue;
                }
                // Prefer a near parent, then a shallow one - a network that always branches off
                // the newest twig is a single long whip, not a tree.
                float score = Vector2.Distance(Tip(v), want) + v.Generation * 0.35f
                    + v.Children * 0.3f;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = v;
                }
            }
            if (best != null && best.Children >= maxTips)
            {
                return null;
            }
            return best;
        }

        /// <summary>
        /// Tries a handful of headings off the parent and keeps the one that covers most of what
        /// is still bare without burying what is already drawn. This is the whole difference
        /// between coverage and a pile: a candidate that lands on top of its neighbours scores
        /// worse than one that reaches somewhere new, however close to the target it is.
        /// </summary>
        private Vine BestChild(Claim c, GridPos salt, Vine parent, Vector2 want, ref int seed)
        {
            Vine best = null;
            float bestScore = 0.001f;
            // A CHAIN, NOT ALWAYS A BRANCH. Every child being one generation smaller means the
            // network runs out of reach before it runs out of claim: a fifth-generation twig can
            // only cross half a cell, so the far side of anything bigger than a 2x2 was simply
            // unreachable and the whole growth piled up by the entry. When the bare spot is
            // further off than this segment can throw, the SAME generation continues from its TIP
            // instead - which is what a root does, and what the sheet's frames are for.
            bool far = Vector2.Distance(Tip(parent), want) > parent.Size * 0.55f;
            int gen = far
                ? parent.Generation
                : Mathf.Min(parent.Generation + 1, Style.GenerationScale.Length - 1);
            for (int k = 0; k < Style.Candidates; k++)
            {
                seed++;
                float along = Mathf.Lerp(Style.BranchAtMin, Style.BranchAtMax,
                    Random01(salt, 700 + seed));
                Vector2 foot = far ? Tip(parent) : Vector2.Lerp(parent.Foot, parent.Mass, along);
                Vector2 aim = want;
                if (k > 0)
                {
                    float swing = (Random01(salt, 760 + seed) - 0.5f) * 2f * Style.BranchSwing;
                    Vector2 d = want - foot;
                    aim = foot + Rotate(d, swing);
                }
                Vine cand = Make(c, salt, foot, aim, seed, gen, parent);
                float gain = 0f, buried = 0f, outside = 0f;
                for (int i = 0; i < c.Field.Count; i++)
                {
                    if (Hits(cand, c.Field[i]))
                    {
                        if (c.Filled[i])
                        {
                            buried += 1f;
                        }
                        else
                        {
                            gain += c.Want[i];
                        }
                    }
                }
                // How much of the segment fell off the claim entirely - a branch mostly in the
                // outside space is decoration, not coverage.
                outside = Mathf.Max(0f, 1f - (gain + buried) / 8f);
                float score = gain - buried * Style.OverlapPenalty
                    - outside * Style.OutsidePenalty;
                if (score > bestScore)
                {
                    if (best != null)
                    {
                        Discard(best);
                    }
                    bestScore = score;
                    best = cand;
                }
                else
                {
                    Discard(cand);
                }
            }
            return best;
        }

        private static Vector2 Rotate(Vector2 v, float degrees)
        {
            float a = degrees * Mathf.Deg2Rad;
            float ca = Mathf.Cos(a), sa = Mathf.Sin(a);
            return new Vector2(v.x * ca - v.y * sa, v.x * sa + v.y * ca);
        }

        /// <summary>Plants a segment and adds it - the backbone's own entry point.</summary>
        private Vine Grow(Claim c, GridPos salt, Vector2 foot, Vector2 toward, int k, int gen,
            float start, Vine parent)
        {
            Vine v = Make(c, salt, foot, toward, k, gen, parent);
            v.Start = start;
            c.Vines.Add(v);
            Absorb(c, v);
            return v;
        }

        /// <summary>
        /// ONE SEGMENT OF THE NETWORK. Its generation decides its size, the frame it settles on
        /// and how fast it gets there; its parent decides where it is planted and when it starts.
        /// Nothing here is turned by a random angle - a drawing that grows left to right says
        /// nothing at all once it can face any way.
        /// </summary>
        private Vine Make(Claim c, GridPos salt, Vector2 foot, Vector2 toward, int k, int gen,
            Vine parent)
        {
            Vector2 d = toward - foot;
            float aim = d.sqrMagnitude > 1e-6f ? Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg : 0f;
            bool flip = Random01(salt, 380 + k) > 0.5f;
            float angle = aim - (flip ? -Style.ArtRise : Style.ArtRise)
                + (Random01(salt, 300 + k) - 0.5f) * 2f * Style.VineTiltSpread;
            float basis = cellSize * Style.VineReach * Style.GenerationScale[gen];
            float want = d.magnitude / Mathf.Max(Style.MassAt, 0.01f);
            float size = Mathf.Clamp(want, basis * 0.85f, basis * 1.25f)
                * (1f + (Random01(salt, 360 + k) - 0.5f) * Style.VineSizeSpread);
            float mass = (angle + (flip ? -Style.ArtRise : Style.ArtRise)) * Mathf.Deg2Rad;
            int settle = Style.GenerationFrame[gen] - 1;
            var v = new Vine
            {
                Foot = foot,
                Mass = foot + new Vector2(Mathf.Cos(mass), Mathf.Sin(mass))
                    * (Style.MassAt * size),
                Angle = angle,
                Flip = flip,
                Size = size,
                Role = Mathf.Min(gen, 2),
                Generation = gen,
                Settle = Mathf.Clamp(settle, 0, FrameCount - 1),
                Parent = parent,
                Duration = Style.GenerationFrameTime[gen] * (settle + 1)
            };
            if (parent != null)
            {
                parent.Children++;
                v.Start = parent.Start + parent.Duration * Style.ChildSpawnAt;
            }
            Dress(v, v.Role);
            return v;
        }

        private Ground BuildGround(GridPos cell, int index)
        {
            var g = new Ground { Cell = cell, Distance = index };
            if (Layers.ShowCorners)
            {
                for (int i = 0; i < 4; i++)
                {
                    g.Corners[i] = Rent(TalismanShapes.Corner(i), CornerOrder, SpiritMaterial());
                }
            }
            g.IdleWait = Style.IdleMinInterval
                + Random01(cell, 11) * (Style.IdleMaxInterval - Style.IdleMinInterval);
            return g;
        }

        private void ClearHarvest()
        {
            for (int i = 0; i < harvests.Count; i++)
            {
                Return(harvests[i].Ghost);
                Return(harvests[i].Kernel);
                for (int k = 0; k < harvests[i].Shards.Count; k++)
                {
                    Return(harvests[i].Shards[k]);
                }
            }
            harvests.Clear();
            harvestClock = -1f;
        }

        private void ClearClaims()
        {
            // Whatever is still blank goes back, whether the reveal finished or was cut off: a
            // held cell nobody releases is a hole in the board for the rest of the round.
            ReleaseFloor();
            for (int i = 0; i < claims.Count; i++)
            {
                Claim c = claims[i];
                Return(c.EdgeRune);
                Return(c.EdgeRecess);
                Return(c.StainBody);
                Return(c.StainDrain);
                c.StainBody = null;
                c.StainDrain = null;
                if (c.Stain != null)
                {
                    // Baked per claim, so it dies with the claim - leaving it to the GC leaks a
                    // texture every time the power fires.
                    c.Stain.Dispose();
                    c.Stain = null;
                }
                ReturnThreads(c.Tendrils);
                ReturnThreads(c.Streams);
                for (int k = 0; k < c.Pockets.Count; k++)
                {
                    ReturnThread(c.Pockets[k].Mesh);
                }
                c.Pockets.Clear();
                ReturnAll(c.Marks);
                c.Arrival.Clear();
                c.Field.Clear();
                c.Filled.Clear();
                c.Given.Clear();
                c.Want.Clear();
                ReturnAll(c.Seeds);
                for (int k = 0; k < c.Vines.Count; k++)
                {
                    Return(c.Vines[k].Body);
                    Return(c.Vines[k].Shadow);
                    Return(c.Vines[k].Depth);
                }
                c.Vines.Clear();
            }
            claims.Clear();
        }

        private void ReturnThreads(List<Thread> threads)
        {
            for (int i = 0; i < threads.Count; i++)
            {
                ReturnThread(threads[i].Mesh);
            }
            threads.Clear();
        }

        private void ClearGround()
        {
            for (int i = 0; i < ground.Count; i++)
            {
                for (int k = 0; k < 4; k++)
                {
                    Return(ground[i].Corners[k]);
                }
                    ReturnAll(ground[i].Flakes);
            }
            ground.Clear();
        }

        private void ReturnAll(List<SpriteRenderer> list)
        {
            for (int i = 0; i < list.Count; i++)
            {
                Return(list[i]);
            }
            list.Clear();
        }

        // =================================================================== the clock

        private void Update()
        {
            if (harvests.Count == 0 && claims.Count == 0 && ground.Count == 0)
            {
                return;
            }
            float dt = Time.deltaTime;
            // THE VINES GROW ON THEIR OWN CLOCK. It runs on past the end of the growth so the
            // idle beats below have a stable base to work from, and it is what every runner's
            // window is measured against.
            for (int i = 0; i < claims.Count; i++)
            {
                claims[i].Clock += dt;
                Crawl(claims[i], dt);
            }
            if (harvestClock >= 0f)
            {
                harvestClock += dt;
                float last = 0f;
                for (int i = 0; i < harvests.Count; i++)
                {
                    last = Mathf.Max(last, harvests[i].Start);
                }
                if (harvestClock > last + Style.HarvestCharge + Style.HarvestRupture + 0.2f)
                {
                    ClearHarvest();
                }
            }
            if (revealClock >= 0f)
            {
                revealClock += dt;
                ReleaseUncovered();
                if (revealClock > RevealSpan() + 0.2f)
                {
                    revealClock = -1f;
                    // The cover has done its work and gone; the corner runes stay.
                    ClearClaims();
                }
            }
            if (recallClock >= 0f)
            {
                recallClock += dt;
                if (recallClock > RecallSpan() + 0.2f)
                {
                    recallClock = -1f;
                    ClearGround();
                }
            }
            else if (revealClock < 0f)
            {
                // THE RAREST IDLE IN THE GAME. One corner warms, then the one across from it, and
                // then nothing for the better part of ten seconds.
                for (int i = 0; i < ground.Count; i++)
                {
                    Ground g = ground[i];
                    if (g.IdleClock >= 0f)
                    {
                        g.IdleClock += dt;
                        if (g.IdleClock > Style.IdleDuration)
                        {
                            g.IdleClock = -1f;
                            g.IdleCorner = (g.IdleCorner + 1) % 4;
                            g.IdleWait = Style.IdleMinInterval
                                + Random01(g.Cell, Mathf.RoundToInt(Time.time * 3f))
                                    * (Style.IdleMaxInterval - Style.IdleMinInterval);
                        }
                    }
                    else
                    {
                        g.IdleWait -= dt;
                        if (g.IdleWait <= 0f)
                        {
                            g.IdleClock = 0f;
                        }
                    }
                }
            }
            Paint();
        }

        /// <summary>
        /// HOW LONG THE COVER TAKES TO COME OFF - and it follows the COVER, not a constant.
        ///
        /// It used to be a flat 0.26s plus a little per cell, which was fine when the claim grew
        /// in about the same time. The network grows in up to ClaimCap, so the reverse was being
        /// crammed into under a third of that: the whole thing blinked out in a couple of frames
        /// and read as no animation at all. The reveal IS the growth backwards, so its length has
        /// to be the growth's length.
        /// </summary>
        private float RevealSpan()
        {
            float longest = 0f;
            for (int i = 0; i < claims.Count; i++)
            {
                longest = Mathf.Max(longest, claims[i].Span);
            }
            if (longest <= 0f)
            {
                longest = Style.VineGrow;
            }
            return Mathf.Min(Style.RevealTotalCap, longest * Style.RetractRate + Style.RevealLift);
        }

        private float RecallSpan()
        {
            return Mathf.Min(Style.RecallTotalCap,
                Style.RecallDim + Style.RecallFlight
                    + Style.RecallPerCell * Mathf.Max(0, ground.Count - 1));
        }

        /// <summary>
        /// THE IDLE DARK CRAWL: a vein of darkness travelling inside the stain, every several
        /// seconds, and only once the claim has finished arriving and is standing still.
        ///
        /// It is the seal's own sweep at a twentieth of the strength. There is no second idle
        /// system and no whole-area pulse: a region that breathes as one is a UI element.
        /// </summary>
        private void Crawl(Claim c, float dt)
        {
            if (revealClock >= 0f || c.Clock < c.Span + Style.SealPulse * 1.6f
                || c.Cells.Count == 0)
            {
                return;
            }
            if (c.CrawlClock >= 0f)
            {
                c.CrawlClock += dt;
                if (c.CrawlClock > Style.CrawlDuration)
                {
                    c.CrawlClock = -1f;
                    c.CrawlWait = Style.CrawlMinInterval
                        + Random01(c.Cells[0], Mathf.RoundToInt(Time.time * 7f))
                            * (Style.CrawlMaxInterval - Style.CrawlMinInterval);
                }
                return;
            }
            c.CrawlWait -= dt;
            if (c.CrawlWait <= 0f)
            {
                c.CrawlClock = 0f;
            }
        }

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

        private void Paint()
        {
            if (toWorld == null)
            {
                return;
            }
            for (int i = 0; i < harvests.Count; i++)
            {
                PaintHarvest(harvests[i]);
            }
            // During a reveal the cover is being pulled back TIP FIRST, and the ground under it
            // appears in the same window - the vine lifts the cloth off.
            // ONE CLOCK FOR BOTH: the retract runs over exactly the span the reveal was given,
            // so the number that decides how long it lasts and the number the vines are unwound
            // against cannot drift apart - which is what left the cover finished while the clock
            // was still running, and then running while the cover was already gone.
            float retract = revealClock < 0f ? 0f : Ease(Span(revealClock, 0f, RevealSpan()));
            for (int i = 0; i < claims.Count; i++)
            {
                PaintClaim(claims[i], retract);
            }
            for (int i = 0; i < ground.Count; i++)
            {
                PaintGround(ground[i]);
            }
        }

        /// <summary>
        /// One ghost being taken. It lights from inside in its OWN colour, its shell comes apart
        /// into soft flakes that keep that colour for a moment before turning spectral, and the
        /// kernel it leaves either settles into a seed or scatters.
        /// </summary>
        private void PaintHarvest(Harvest h)
        {
            float t = harvestClock - h.Start;
            Vector2 at = toWorld(h.Cell);
            float cube = cellSize * 0.98f;
            float charge = Span(t, 0f, Style.HarvestCharge);
            float burst = Span(t, Style.HarvestCharge,
                Style.HarvestCharge + Style.HarvestRupture);

            if (h.Ghost != null)
            {
                // It brightens from within and draws in a little - never a scale-up, which reads
                // as a pop rather than a charge.
                float shrink = 1f - 0.06f * Ease(charge) - 0.5f * Ease(burst);
                h.Ghost.transform.localPosition = new Vector3(at.x, at.y, 0f);
                Fit(h.Ghost, cube * shrink, cube * shrink);
                float lift = 1f + 0.5f * Ease(charge);
                h.Ghost.color = new Color(
                    Mathf.Clamp01(h.Colour.r * lift), Mathf.Clamp01(h.Colour.g * lift),
                    Mathf.Clamp01(h.Colour.b * lift), (1f - Ease(burst)) * 0.85f);
            }
            for (int i = 0; i < h.Shards.Count; i++)
            {
                SpriteRenderer r = h.Shards[i];
                if (r == null)
                {
                    continue;
                }
                float k = Ease(burst);
                Vector2 p = at + h.ShardDirs[i] * (k * cellSize * Style.ShardSpread);
                r.transform.localPosition = new Vector3(p.x, p.y, 0f);
                r.transform.localRotation = Quaternion.Euler(0f, 0f, i * 47f + k * 40f);
                float s = cellSize * Style.ShardSize * (1f - k * 0.4f);
                Fit(r, s, s * 0.8f);
                // ITS OWN COLOUR FIRST. A shard that is spectral cyan from frame one throws away
                // the only thing that makes this that block's death.
                float spectral = Ease(Span(k, Style.ShardKeepsColour, 1f));
                PaintSpirit(r, h.Colour, h.Colour, 0.6f, 0.5f, 0f, spectral,
                    (1f - Ease(Span(k, 0.7f, 1f))) * 0.9f);
            }
            if (h.Kernel != null)
            {
                float grow = Span(t, Style.HarvestCharge * 0.6f,
                    Style.HarvestCharge + Style.HarvestRupture * 0.5f);
                float after = Span(t, Style.HarvestCharge + Style.HarvestRupture * 0.5f,
                    Style.HarvestCharge + Style.HarvestRupture);
                // 110% then settling to a stable seed - or scattering, where nothing was claimed.
                float scale = Mathf.Lerp(0f, 1.1f, Ease(grow))
                    * (h.Reclaimable ? Mathf.Lerp(1f, 0.72f, Ease(after))
                        : Mathf.Lerp(1f, 0f, Ease(after)));
                float s = cellSize * Style.KernelSize * scale;
                h.Kernel.transform.localPosition = new Vector3(at.x, at.y, 0f);
                Fit(h.Kernel, s, s * 1.1f);
                PaintSpirit(h.Kernel, Style.VineBody, h.Colour, 0.8f, 0.6f,
                    0.55f + 0.35f * Ease(grow), h.Reclaimable ? 0.15f : Ease(after),
                    Mathf.Clamp01(scale * 2f));
            }
        }

        /// <summary>
        /// ONE COMPONENT'S VINE SYSTEM, growing. Every runner has its own window in the clock, so
        /// the main stem is out of the board's edge and half way across before the first branch
        /// leaves it, and the curls inside the cells are last.
        ///
        /// What is drawn here is deliberately NOT a floor: there is no tile, no bevel and no
        /// contact shadow under any of it, because the ground does not exist until next round and
        /// a player who thinks it does will waste a turn finding out.
        /// </summary>
        private void PaintClaim(Claim c, float retract)
        {
            float lift = cellSize * Style.ClaimLift;

            // THE EDGE ANSWERS FIRST: a small mark on the board own rim. Without it the cover
            // starts in mid-air and reads as decoration laid over the outside space rather than as
            // something the board itself put out.
            if (c.EdgeRecess != null)
            {
                // THE RECESS OPENS FIRST, and it is DARKER than the board rather than brighter -
                // a split in a surface is a hole in it, and a bright mark there is a badge stuck
                // on it. It widens as it opens instead of fading in.
                float open = Ease(Span(c.Clock, 0f, Style.EdgeWake * 0.7f)) * (1f - retract);
                float s1 = cellSize * Style.OriginSize;
                c.EdgeRecess.transform.localPosition =
                    new Vector3(c.Origin.x, c.Origin.y + lift, 0f);
                c.EdgeRecess.transform.localRotation =
                    Quaternion.Euler(0f, 0f, Random01(c.Cells[0], 9) * 360f);
                Fit(c.EdgeRecess, s1 * Mathf.Lerp(0.35f, 1f, open), s1 * 0.82f * open);
                c.EdgeRecess.color = new Color(Style.OriginRecess.r, Style.OriginRecess.g,
                    Style.OriginRecess.b, 0.85f * open);
            }
            if (c.EdgeRune != null)
            {
                // AND THE CRACK LIGHTS ACROSS IT a beat later, which is the order that makes it
                // read as the board being opened rather than as a rune being drawn on it.
                float wake = Ease(Span(c.Clock, Style.EdgeWake * 0.45f, Style.EdgeWake * 1.3f))
                    * (1f - retract);
                float s2 = cellSize * Style.OriginSize * 0.62f * wake;
                c.EdgeRune.transform.localPosition =
                    new Vector3(c.Origin.x, c.Origin.y + lift, 0f);
                Fit(c.EdgeRune, s2, s2 * 0.85f);
                PaintSpirit(c.EdgeRune, Style.RuneDeep, Style.RuneBody, 0.7f, 0.5f,
                    0.45f * wake, 0f, wake);
            }
            // THE CURSE STAIN. One field, two blends, and NOTHING HERE SAYS WHEN: every texel
            // carries its own moment, so the whole growth is a front moving over it - the patches
            // opening from their middles, the bridges closing from both ends at once, the tongues
            // last. Run the front backwards and that is the unseal, in reverse order, for free.
            float scale = c.Stain != null ? Mathf.Max(c.Stain.Scale, 1e-4f) : 1f;
            float front = retract > 0f
                ? Mathf.Lerp(1.1f, -0.15f, Ease(retract))
                : c.Clock / scale;
            // THE SEAL CLOSING is a CONTRACTION, not a flash: the mass tightens a couple of per
            // cent from its rim and settles.
            float shut = Ease(Span(c.Clock, c.Span, c.Span + Style.SealPulse * 0.55f))
                * (1f - Ease(Span(c.Clock, c.Span + Style.SealPulse * 0.7f,
                    c.Span + Style.SealPulse * 1.4f)));
            float tighten = Layers.ShowSeal ? Style.SealTighten * shut : 0f;
            // THE ENERGY, once, and the IDLE CRAWL, rarely: the same band travelling along the
            // growth coordinate. Because that coordinate IS the order the vines went in, what the
            // gold passes over is what the plant passed over, in the same order, without a second
            // timeline existing anywhere.
            float sweep = -1f;
            float gain = 0f;
            if (Layers.ShowSeal && c.Clock >= c.Span
                && c.Clock <= c.Span + Style.SealPulse * 1.5f)
            {
                sweep = Span(c.Clock, c.Span, c.Span + Style.SealPulse * 1.2f) * 1.25f;
                gain = Style.SealSweep;
            }
            else if (c.CrawlClock >= 0f)
            {
                float k = Mathf.Clamp01(c.CrawlClock / Style.CrawlDuration);
                sweep = k * 1.25f;
                gain = Style.CrawlGain * Mathf.Sin(k * Mathf.PI);
            }
            // The colour goes out a breath BEFORE the darkness lands, which is the difference
            // between the vine putting the light out and a decal arriving.
            PaintStain(c, c.StainDrain, true, front + TalismanStain.Style.DrainLead / scale,
                tighten, sweep, gain, lift);
            PaintStain(c, c.StainBody, false, front, tighten, sweep, gain, lift);

            // THE SHADOW ROOTS crawling out of the network into the rest of the claim. They grow
            // by being EXTENDED and retract by being pulled back, tip first - the same two
            // numbers moving, never an alpha fade.
            for (int i = 0; i < c.Tendrils.Count; i++)
            {
                Thread t = c.Tendrils[i];
                float on = Ease(Span(c.Clock, t.Start, t.Start + t.Duration));
                if (retract > 0f)
                {
                    float from = Mathf.Max(0f, c.Span - (t.Start + t.Duration));
                    on *= 1f - Ease(Span(retract * c.Span, from, from + t.Duration));
                }
                float deep = TalismanTendril.Style.TendrilOpacity
                    * (t.Generation == 0 ? 1f : 0.78f)
                    * (1f + Style.SealSweep * 4f * shut);
                t.Mesh.Ribbon(t.From, t.Control, t.To, t.Width,
                    TalismanTendril.Style.TendrilTaper, 0f, on,
                    new Color(TalismanTendril.Style.Tendril.r, TalismanTendril.Style.Tendril.g,
                        TalismanTendril.Style.Tendril.b, deep));
            }
            // THE SKINS over the holes they leave. They are pulled OPEN from their middle rather
            // than faded in: a skin appears by being stretched.
            for (int i = 0; i < c.Pockets.Count; i++)
            {
                Pocket p = c.Pockets[i];
                float on = Ease(Span(c.Clock, p.Start,
                    p.Start + TalismanTendril.Style.VeilGrow));
                if (retract > 0f)
                {
                    // They let go FIRST, before anything is pulled back - the skin releases and
                    // then the roots come out from under it.
                    on *= 1f - Ease(Span(retract, 0.02f, 0.3f));
                }
                p.Mesh.Pocket(p.Anchors, p.Count, Mathf.Lerp(0.35f, 1f, on), p.Sag,
                    new Color(TalismanTendril.Style.Veil.r, TalismanTendril.Style.Veil.g,
                        TalismanTendril.Style.Veil.b,
                        TalismanTendril.Style.VeilOpacity * on));
            }
            // AND THE STREAMS, which exist only while the gift is going home: a stretch of
            // darkness travelling out of the patch and into the vine that is pulling it.
            for (int i = 0; i < c.Streams.Count; i++)
            {
                Thread t = c.Streams[i];
                float k = retract <= 0f ? 0f
                    : Span(retract * c.Span, t.Start, t.Start + t.Duration);
                float len = TalismanTendril.Style.StreamLength;
                float head = k * (1f + len);
                var flow = new Color(TalismanTendril.Style.Tendril.r,
                    TalismanTendril.Style.Tendril.g, TalismanTendril.Style.Tendril.b,
                    TalismanTendril.Style.StreamOpacity
                        * Mathf.Sin(Mathf.Clamp01(k) * Mathf.PI));
                t.Mesh.Ribbon(t.From, t.Control, t.To, t.Width, 0.45f,
                    Mathf.Clamp01(head - len), Mathf.Clamp01(head), flow);
            }
            // DEV ONLY: the cells the rules reclaimed, and the box they sit in. The box is drawn
            // so that "is the darkness following the cells or the rectangle?" can be looked at.
            for (int i = 0; i < c.Marks.Count; i++)
            {
                SpriteRenderer m = c.Marks[i];
                if (m == null)
                {
                    continue;
                }
                if (i < c.Cells.Count)
                {
                    Vector2 cell = toWorld(c.Cells[i]);
                    m.transform.localPosition = new Vector3(cell.x, cell.y, 0f);
                    Fit(m, cellSize, cellSize);
                    m.color = new Color(0.95f, 0.25f, 0.2f, 0.85f);
                    continue;
                }
                float x0 = float.MaxValue, x1 = float.MinValue;
                float y0 = float.MaxValue, y1 = float.MinValue;
                for (int k = 0; k < c.Cells.Count; k++)
                {
                    Vector2 q = toWorld(c.Cells[k]);
                    x0 = Mathf.Min(x0, q.x); x1 = Mathf.Max(x1, q.x);
                    y0 = Mathf.Min(y0, q.y); y1 = Mathf.Max(y1, q.y);
                }
                m.transform.localPosition = new Vector3((x0 + x1) * 0.5f, (y0 + y1) * 0.5f, 0f);
                Fit(m, x1 - x0 + cellSize, y1 - y0 + cellSize);
                m.color = new Color(0.95f, 0.85f, 0.2f, 0.6f);
            }
            for (int i = 0; i < c.Vines.Count; i++)
            {
                PaintVine(c.Vines[i], c.Clock, c.Span, retract, lift, Seal(c, c.Vines[i]));
            }
            for (int i = 0; i < c.Seeds.Count && i < c.Cells.Count; i++)
            {
                SpriteRenderer seed = c.Seeds[i];
                if (seed == null)
                {
                    continue;
                }
                Vector2 at = toWorld(c.Cells[i]);
                // It is there from the harvest and the cover grows over it; it does not appear
                // when the cover arrives.
                float when = Style.EdgeWake
                    + (c.Cells.Count > 1 ? i / (float)(c.Cells.Count - 1) : 0f)
                        * Mathf.Max(0f, c.Span - Style.EdgeWake - Style.VineGrow);
                // It goes under the stain as the cover closes over it, so it is not a bright
                // dot sitting on top of a mass that is meant to be hiding things.
                float taken = Ease(Span(c.Clock, when, when + Style.VineGrow));
                float pulse = 1f + 0.12f * Mathf.Sin(Time.time * 2.2f + i);
                float s4 = cellSize * Style.SeedSize * pulse * (1f - taken * 0.35f);
                seed.transform.localPosition = new Vector3(at.x, at.y + lift, 0f);
                Fit(seed, s4 * 0.82f, s4);
                PaintSpirit(seed, Style.VineDeep, Style.RuneBody, 0.65f, 0.7f,
                    0.45f + 0.3f * taken, 0f, 1f - retract);
            }
        }

        /// <summary>
        /// One vine, at whatever frame its own growth has reached.
        ///
        /// GOING BACK IS THE SAME ANIMATION RUN BACKWARDS, and in the reverse ORDER too: the last
        /// vine to grow is the first to let go, so the cover peels off the board the way it
        /// crawled on and the ground underneath is uncovered from the far side inwards. Fading the
        /// whole cover out instead would be a dissolve, and a dissolve says the vines were never
        /// really there.
        /// </summary>
        /// <summary>
        /// THE SEAL RUNNING THROUGH ONE SEGMENT, 0 before it arrives, 1 at its peak, 0 after.
        ///
        /// It leaves the root origin when the growth finishes and travels out by GENERATION, so
        /// the trunk warms first and the twigs last - the talisman locking the thing it grew. And
        /// it is a warmth in the vine's OWN colour, not a light laid over it: the region ends the
        /// beat darker than it started, because this is a lock closing rather than a firework.
        /// </summary>
        private float Seal(Claim c, Vine v)
        {
            if (!Layers.ShowSeal)
            {
                return 0f;
            }
            float at = c.Span + v.Generation * Style.SealPulse * 0.22f;
            return Ease(Span(c.Clock, at, at + Style.SealPulse * 0.45f))
                * (1f - Ease(Span(c.Clock, at + Style.SealPulse * 0.5f,
                    at + Style.SealPulse * 1.2f)));
        }

        private void PaintVine(Vine v, float clock, float span, float retract, float lift,
            float seal)
        {
            if (v.Body == null)
            {
                return;
            }
            Sprite[] fs = Frames();
            if (fs == null)
            {
                Vanish(v);
                return;
            }
            float grow = Ease(Span(clock, v.Start, v.Start + v.Duration));
            if (retract > 0f)
            {
                // Where this vine sits when the whole span is read backwards.
                float from = Mathf.Max(0f, span - (v.Start + v.Duration));
                grow *= 1f - Ease(Span(retract * span, from, from + v.Duration));
            }
            if (grow <= 0f)
            {
                Vanish(v);
                return;
            }
            // IT SETTLES ON ITS OWN LAST FRAME, and a secondary's is one earlier: the sheet's
            // final frame is a full tangle, and three of those in a claim is three heroes.
            int stop = Mathf.Clamp(v.Settle, 0, fs.Length - 1);
            Sprite frame = fs[Mathf.Clamp(Mathf.FloorToInt(grow * (stop + 1)), 0, stop)];
            var turn = Quaternion.Euler(0f, 0f, v.Angle);
            var scale = new Vector3(v.Size, v.Size, 1f);

            v.Body.sprite = frame;
            v.Body.flipY = v.Flip;
            v.Body.transform.localPosition = new Vector3(v.Foot.x, v.Foot.y + lift, 0f);
            v.Body.transform.localRotation = turn;
            v.Body.transform.localScale = scale;
            // The gold runs THROUGH it and the whole network settles a shade darker behind the
            // pulse - the art's own runes are what catch the warmth, because the tint is carried
            // in the sprite's own colour rather than painted over it.
            float dim = 1f - Style.SealDarken * Ease(Span(clock, span, span + Style.SealPulse));
            var body = new Color(dim, dim, dim, 1f);
            v.Body.color = seal > 0f
                ? Color.Lerp(body, Style.SealGold, seal * Style.SealWarmth)
                : body;

            if (v.Shadow != null)
            {
                // The drop is in WORLD terms - a shadow does not turn with the thing casting it -
                // so it is added after the rotation rather than inside it.
                float drop = cellSize * Style.ShadowOffset;
                v.Shadow.sprite = frame;
                v.Shadow.flipY = v.Flip;
                v.Shadow.transform.localPosition =
                    new Vector3(v.Foot.x + drop * 0.6f, v.Foot.y + lift - drop * 0.8f, 0f);
                v.Shadow.transform.localRotation = turn;
                v.Shadow.transform.localScale = scale * Style.ShadowSpread;
                // A BRANCH PRESSES LESS HARD THAN THE TRUNK IT CAME OFF. It is also the layer
                // most likely to be lying over another shadow, and stacked contact shadows are
                // what turn the back of a claim into a black smear.
                // A twig presses less hard than the trunk it came off, and it is also the layer
                // most likely to be lying over another shadow.
                v.Shadow.color = new Color(0.01f, 0.03f, 0.04f,
                    Style.ShadowStrength * Mathf.Pow(0.78f, v.Generation));
            }
            if (v.Depth != null)
            {
                // THE CURSE DEPTH, and it is a different thing from the shadow above it: the
                // shadow is offset and tight and says the vine is LYING on something, while this
                // is centred, a little wider and says the ground under it is darker for the vine
                // being there. Both, or the plant either floats or drags a smear.
                //
                // It is scaled about the plant's own MASS rather than about its foot, so the
                // swell stays centred on the drawing instead of sliding a sixth of a cell off it.
                Vector2 keep = v.Foot + (1f - Style.DepthSpread) * (v.Mass - v.Foot);
                v.Depth.sprite = frame;
                v.Depth.flipY = v.Flip;
                v.Depth.transform.localPosition = new Vector3(keep.x, keep.y + lift, 0f);
                v.Depth.transform.localRotation = turn;
                v.Depth.transform.localScale = scale * Style.DepthSpread;
                v.Depth.color = new Color(Style.Depth.r, Style.Depth.g, Style.Depth.b,
                    Style.DepthStrength * Mathf.Pow(0.85f, v.Generation));
            }
        }

        /// <summary>One vine off the screen, all three of its layers at once - which is the point
        /// of it being one call: a vine that leaves its own darkness behind is a hole.</summary>
        private static void Vanish(Vine v)
        {
            if (v.Body != null)
            {
                v.Body.color = Clear;
            }
            if (v.Shadow != null)
            {
                v.Shadow.color = Clear;
            }
            if (v.Depth != null)
            {
                v.Depth.color = Clear;
            }
        }

        /// <summary>
        /// ONE OF THE STAIN'S TWO PASSES.
        ///
        /// Same sprite, same front, same contraction - the only difference is the blend the
        /// material was built with, and whether the seal's sweep applies. Everything that varies
        /// per claim goes through a property block, so both passes of every claim in the scene
        /// share one material each.
        /// </summary>
        private void PaintStain(Claim c, SpriteRenderer r, bool drain, float front, float tighten,
            float sweep, float gain, float lift)
        {
            if (r == null || c.Stain == null)
            {
                return;
            }
            r.transform.localPosition = new Vector3(c.Stain.At.x, c.Stain.At.y + lift, 0f);
            r.transform.localRotation = Quaternion.identity;
            Fit(r, c.Stain.Size.x, c.Stain.Size.y);
            if (StainMaterial() == null)
            {
                // NO SHADER. The field was then baked with a plain white body, so it can still be
                // drawn - it just arrives as one piece instead of crawling, and the drain, which
                // is a blend mode rather than a colour, cannot happen at all. It still says the
                // ground is taken, which is the sentence that matters.
                if (drain)
                {
                    r.color = Clear;
                    return;
                }
                r.color = new Color(TalismanStain.Style.Stain.r, TalismanStain.Style.Stain.g,
                    TalismanStain.Style.Stain.b,
                    TalismanStain.Style.Centre * Mathf.Clamp01(front));
                return;
            }
            r.color = Color.white;
            block.Clear();
            block.SetFloat(FrontId, front);
            block.SetFloat(BandId, TalismanStain.Style.Band / Mathf.Max(c.Stain.Scale, 1e-4f));
            block.SetFloat(ContractId, tighten);
            block.SetFloat(SweepId, sweep);
            block.SetFloat(SweepWidthId, Style.SealSweepWidth);
            block.SetFloat(SweepGainId, drain ? 0f : gain);
            block.SetFloat(DebugId, !drain && Layers.DebugParts ? 1f : 0f);
            r.SetPropertyBlock(block);
        }

        private void PaintGround(Ground g)
        {
            Vector2 at = toWorld(g.Cell);
            // THE RUNE COMES UP WHERE THE COVER HAS ACTUALLY LEFT, not on a stagger of its own.
            // It used to run off a per-cell delay, which is a second timeline beside the one the
            // stain is drawing with - and two timelines drift, so a rune could arrive from under
            // a patch that was still standing.
            float appear = Ease(Uncover(g.Cell));
            float leaving = recallClock < 0f
                ? 0f
                : Ease(Span(recallClock, Style.RecallDim + g.Distance * Style.RecallPerCell,
                    Style.RecallDim + g.Distance * Style.RecallPerCell + Style.RecallFlight));

            for (int i = 0; i < 4; i++)
            {
                SpriteRenderer r = g.Corners[i];
                if (r == null)
                {
                    continue;
                }
                float fx = i == 1 || i == 2 ? 1f : -1f;
                float fy = i == 0 || i == 1 ? 1f : -1f;
                float inset = cellSize * 0.5f * Style.CornerInset;
                r.transform.localPosition = new Vector3(at.x + fx * inset, at.y + fy * inset, 0f);
                // The baked stroke has its elbow at its own top-left, so each corner is that
                // sprite mirrored into place - a set of four, not one rotated four times.
                float s = cellSize * Style.CornerSize;
                r.transform.localRotation = Quaternion.identity;
                r.transform.localScale = new Vector3(
                    s / Mathf.Max(r.sprite.bounds.size.x, 0.0001f) * fx * -1f,
                    s / Mathf.Max(r.sprite.bounds.size.y, 0.0001f) * fy * -1f, 1f);
                // ONE corner warms, then the one across from it. Never the whole cell.
                float warm = 0f;
                if (g.IdleClock >= 0f)
                {
                    float k = Mathf.Clamp01(g.IdleClock / Style.IdleDuration);
                    bool first = i == g.IdleCorner;
                    bool second = i == (g.IdleCorner + 2) % 4;
                    if (first)
                    {
                        warm = Mathf.Sin(Mathf.Clamp01(k / 0.5f) * Mathf.PI);
                    }
                    else if (second)
                    {
                        warm = Mathf.Sin(Mathf.Clamp01((k - 0.4f) / 0.6f) * Mathf.PI);
                    }
                }
                PaintSpirit(r, Style.RuneDeep, Style.RuneBody, 0.7f, 0.35f,
                    Style.IdleWarm * warm, leaving * 0.8f,
                    Style.CornerOpacity * appear * (1f - leaving));
            }
            // And the flakes it comes apart into, drawn home along the way the vines came.
            for (int i = 0; i < g.Flakes.Count; i++)
            {
                SpriteRenderer r = g.Flakes[i];
                if (r == null)
                {
                    continue;
                }
                float a = (i * 2.39996f) % (Mathf.PI * 2f);
                // Toward the board, not outward: the power is COLLECTING what it lent.
                Vector2 home = -new Vector2(Mathf.Sign(g.Cell.X + 0.5f), 0f);
                Vector2 p = at + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (cellSize * 0.12f)
                    + home * (Ease(leaving) * cellSize * 0.7f);
                r.transform.localPosition = new Vector3(p.x, p.y + Ease(leaving) * cellSize * 0.05f,
                    0f);
                r.transform.localRotation = Quaternion.Euler(0f, 0f, a * Mathf.Rad2Deg
                    + leaving * 30f);
                float s = cellSize * Style.FlakeSize * (1f - leaving * 0.5f);
                Fit(r, s, s * 0.8f);
                PaintSpirit(r, Style.VineBody, Style.RuneBody, 0.5f, 0.4f, 0.2f, leaving,
                    Ease(Span(leaving, 0f, 0.2f)) * (1f - Ease(Span(leaving, 0.6f, 1f))));
            }
        }

        private static readonly List<SpriteRenderer> EmptyList = new List<SpriteRenderer>();

        private static readonly Color Clear = new Color(1f, 1f, 1f, 0f);

        /// <summary>Paints one piece of the talisman's substance.</summary>
        private void PaintSpirit(SpriteRenderer r, Color deep, Color body, float tone, float bevel,
            float heat, float spectral, float alpha)
        {
            if (r == null)
            {
                return;
            }
            r.color = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha));
            if (SpiritMaterial() == null)
            {
                // No shader: flat jade or gold, which still tells the story.
                r.color = new Color(body.r, body.g, body.b, Mathf.Clamp01(alpha));
                return;
            }
            block.Clear();
            block.SetColor(DeepId, deep);
            block.SetColor(BodyId, body);
            block.SetColor(HiId, heat > 0f ? Style.RuneHigh : Style.VineHigh);
            block.SetColor(GhostId, Style.Spectral);
            block.SetFloat(ToneId, tone);
            block.SetFloat(BevelId, bevel);
            block.SetFloat(HeatId, heat);
            block.SetColor(HeatColourId, Style.RuneHigh);
            block.SetFloat(SpectralId, spectral);
            block.SetFloat(RimId, 0.3f);
            block.SetColor(RimColourId, Style.Rim);
            block.SetFloat(FaceHalfId, CompressedCubeView.FaceHalfOf(r));
            r.SetPropertyBlock(block);
        }

        private static readonly int DeepId = Shader.PropertyToID("_Deep");
        private static readonly int BodyId = Shader.PropertyToID("_Body");
        private static readonly int HiId = Shader.PropertyToID("_Hi");
        private static readonly int GhostId = Shader.PropertyToID("_Ghost");
        private static readonly int ToneId = Shader.PropertyToID("_Tone");
        private static readonly int BevelId = Shader.PropertyToID("_Bevel");
        private static readonly int HeatId = Shader.PropertyToID("_Heat");
        private static readonly int HeatColourId = Shader.PropertyToID("_HeatColour");
        private static readonly int SpectralId = Shader.PropertyToID("_Spectral");
        private static readonly int RimId = Shader.PropertyToID("_Rim");
        private static readonly int RimColourId = Shader.PropertyToID("_RimColour");
        private static readonly int FaceHalfId = Shader.PropertyToID("_FaceHalf");

        private static Material spiritMaterial;

        private static bool spiritLooked;

        /// <summary>The TalismanSpirit material, or null - everything is then flat jade and gold.
        /// </summary>
        public static Material SpiritMaterial()
        {
            if (!spiritLooked)
            {
                spiritLooked = true;
                Shader shader = Shader.Find("ProjectBlock/TalismanSpirit");
                if (shader == null)
                {
                    shader = Resources.Load<Shader>("Shaders/TalismanSpirit");
                }
                if (shader != null && shader.isSupported)
                {
                    spiritMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                }
            }
            return spiritMaterial;
        }

        private static readonly int FrontId = Shader.PropertyToID("_Front");
        private static readonly int BandId = Shader.PropertyToID("_Band");
        private static readonly int ContractId = Shader.PropertyToID("_Contract");
        private static readonly int SweepId = Shader.PropertyToID("_Sweep");
        private static readonly int SweepWidthId = Shader.PropertyToID("_SweepWidth");
        private static readonly int SweepGainId = Shader.PropertyToID("_SweepGain");
        private static readonly int DebugId = Shader.PropertyToID("_Debug");

        private static Material stainMaterial;

        private static Material drainMaterial;

        private static bool stainLooked;

        /// <summary>The alpha pass - the darkness itself. Null if the shader is missing, and the
        /// stain then comes up as one flat piece.</summary>
        public static Material StainMaterial()
        {
            LookForStain();
            return stainMaterial;
        }

        /// <summary>
        /// The MULTIPLY pass - the local colour drain.
        ///
        /// Blend DstColor OneMinusSrcAlpha with a premultiplied output gives dst * lerp(1, k, a):
        /// a real multiply of whatever is already in the frame, masked by the same field. This is
        /// the pass that actually takes the light out of the outside space, and no opacity on the
        /// alpha pass can stand in for it - see TalismanStain.Style.Drain for the arithmetic.
        /// </summary>
        public static Material DrainMaterial()
        {
            LookForStain();
            return drainMaterial;
        }

        private static Vector4 Linear(Color c)
        {
            return new Vector4(c.r, c.g, c.b, 1f);
        }

        private static void LookForStain()
        {
            if (stainLooked)
            {
                return;
            }
            stainLooked = true;
            Shader shader = Shader.Find("ProjectBlock/TalismanStain");
            if (shader == null)
            {
                shader = Resources.Load<Shader>("Shaders/TalismanStain");
            }
            if (shader == null || !shader.isSupported)
            {
                return;
            }
            stainMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            drainMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            for (int i = 0; i < 2; i++)
            {
                Material m = i == 0 ? stainMaterial : drainMaterial;
                // SetVector, never SetColor: these two are LINEAR coefficients rather than
                // swatches, and SetColor would convert them out of the space they were measured
                // in. See the note on the shader's own properties.
                m.SetVector("_Stain", Linear(TalismanStain.Style.Stain));
                m.SetVector("_Drain", Linear(TalismanStain.Style.Drain));
                m.SetFloat("_Centre", TalismanStain.Style.Centre);
                m.SetFloat("_Edge", TalismanStain.Style.Edge);
                m.SetFloat("_DrainStrength", TalismanStain.Style.DrainStrength);
                m.SetFloat("_Mode", i);
            }
            stainMaterial.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            stainMaterial.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            drainMaterial.SetFloat("_SrcBlend", (float)BlendMode.DstColor);
            drainMaterial.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        }

        // =================================================================== pooling

        private readonly Stack<SpriteRenderer> spare = new Stack<SpriteRenderer>();

        /// <summary>The soft meshes are pooled like everything else - a claim can want thirty
        /// of them and a power that fires every round must not leave thirty GameObjects behind
        /// each time.</summary>
        private readonly Stack<TalismanTendril> spareThreads = new Stack<TalismanTendril>();

        private TalismanTendril RentThread(int order)
        {
            TalismanTendril t = spareThreads.Count > 0
                ? spareThreads.Pop()
                : TalismanTendril.Make(transform, order);
            t.Order(order);
            t.Hide();
            return t;
        }

        private void ReturnThread(TalismanTendril t)
        {
            if (t == null)
            {
                return;
            }
            t.Hide();
            spareThreads.Push(t);
        }

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
                var go = new GameObject("Talisman");
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
            // A pooled renderer carries its last mirror, and a stale flip is a vine growing out of
            // the wrong side of its own base.
            r.flipX = false;
            r.flipY = false;
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
