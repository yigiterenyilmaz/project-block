// PURPOSE: Overtime ("uzatma") feedback: a ring of drawn FLAMES burning around the arena's
// border. It ignites once the player continues past an advance offer and grows deeper into
// overtime - the fire gets BIGGER and denser. Reset per round via SetState(0, ...).
//
// This replaced a 600-line additive PARTICLE fire. The particle version was soft, glowing and
// emissive, which is the one thing the rest of this board is not: destruction here is flat
// hard-edged squares on a plain white sprite, and the project rule is that a soft texture is
// what would look imported. The drawn flame is flat fills with a black outline, so it speaks
// the board's language instead of fighting it.
//
// THE ART: Assets/Resources/Art/Fx/flame_sheet.png, TWENTY-EIGHT frames on a 7x4 grid, sliced
// at runtime and played in reading order. The frames are one flame growing, guttering and dying
// back - 210px tall at the smallest, 458 at the peak - so playing them straight through and
// wrapping is already a clean loop and nothing here has to ping-pong or ease.
//
// The sheet is REPACKED from what the artist delivers, and the repack is not optional: the
// source lays the frames out on irregular centres with the right-hand column short, which no
// arithmetic can slice. Every frame is moved into an identical cell, its baseline planted a
// fixed 4px above the cell floor, and centred on the horizontal centroid of its BASE rather
// than its bounding box - the tips wave about, and centring on those makes the foot of the
// flame jitter from frame to frame. Nothing is resampled, so the art keeps its own resolution.
//
// A ROW OF LITTLE FLAMES along the bottom lip was built and switched back off (Style.SmallRow).
// It worked - packed shoulder to shoulder, count derived from the space so it held on any arena
// size - but the design came back to the corner pair alone. The sheet and the placement are
// still here behind that flag.
//
// IT IS TWO FLAMES. One on each BOTTOM CORNER of the arena, each standing with its foot
// EXACTLY on its corner point and rising a little past the top of the board. Nothing is nudged
// out of the way for them: no bed under the foot, no sideways clearance. A flame planted on a
// corner is half over the board by definition and does obscure part of the edge column, and
// that is accepted - these two are the effect, not decoration around it.
//
// TWO THINGS HAD TO BE RIGHT for the foot to actually land on the corner, and both were wrong
// at first:
//   * The sprite pivot sits at FootPad, not at the cell floor. The repack plants every frame a
//     fixed 4px above it, and a pivot on the floor hangs the flame that much low. All 28 share
//     that padding exactly, so the foot does not creep as the loop plays.
//   * The flame is planted on the VISIBLE corner, which is not the one WorldRect reports.
//     WorldRect is the cell area; the board's background plate is drawn BoardView.BorderOverhang
//     wider, so the dark edge the player sees is half of that further out. Planting on the grid
//     corner left the foot hovering just above the arena.
//
// THEY ARE AS LARGE AS THE FRAME HOLDS, and no larger. The foot sits at world y -2.425 and the
// view ends at 5, so there are 7.43 units above it and the tallest frame fills 96% of its cell:
// BottomCornerHeightHigh 1.19 is exactly the point where nothing is cut off the top of the
// screen. It sat at 1.50 for a while, where nineteen of the twenty-eight frames ran past it.
//
// A WHOLE RING WAS TRIED FIRST and lost. Flames along every edge, at several densities and
// sizes, with each edge solving its clearance differently; the machinery for it is all still
// here and reachable (Style.EdgeCountLow/High, TopCorners, SideClearance, BottomBed, TopFill)
// but it is switched off. Two facts came out of it worth keeping:
//   * UPRIGHT ONLY. Rotating a flame to its edge's outward normal is the obvious way to keep a
//     ring off the play area. It looks wrong: this art reads as fire only while it points up -
//     on its side it is a splash, upside down it is a drip.
//   * THE TOP HAS NO ROOM. The arena's top edge is at world y 4.15 under a camera ending at 5,
//     so under a unit of headroom. Anything up there is small whether or not you want it to be,
//     which is most of why the ring never balanced against flames this size.
//
// SIZE IS CUT FROM THE ARENA'S SHORT SIDE, never world constants - the arena is 5x5 to 9x9
// across a run. And HEAT SATURATES: level climbs at full rate to FullHeatLevel and at 40% after
// it, so deep overtime keeps escalating without the twelfth level being twice the sixth.
//
// THE TWO ARE NOT IN LOCKSTEP. Each gets its own starting frame, its own playback speed and a
// little size variation, so they breathe against each other rather than as one animation.

using System.Collections.Generic;
using UnityEngine;

namespace ProjectBlock.View
{
    /// <summary>Arena border fire that scales with the round's overtime depth.</summary>
    public sealed class FlameStreakView : MonoBehaviour
    {
        /// <summary>Deepest overtime the fire still reads differently at. Callers pass the raw
        /// continue count; this clamps.</summary>
        public const int MaxLevel = 12;

        // =================================================================== TUNING
        /// <summary>Everything that decides how the fire LOOKS, in one place.</summary>
        public static class Style
        {
            /// <summary>Frames per second of the flame loop. WHAT MATTERS IS THE CYCLE TIME,
            /// not this number: the sheet is one whole grow-and-die cycle, so the flame breathes
            /// once every FrameCount/Fps seconds. The 8-frame sheet this replaced ran at 7, which
            /// is 1.14s a breath, and a breath here takes 3.5s.
            ///
            /// IT SETS SMOOTHNESS AND SPEED TOGETHER, and with a fixed 28 frames there is no
            /// separating them: cycle time is FrameCount/Fps, so slowing the fire down is the
            /// same act as making it step. At 6 a frame was held 167ms, well past the ~100ms
            /// where the eye starts counting them, and it read as choppy; 12 held 83ms and did
            /// not, but ran the fire twice as fast as wanted. 8 sits between them at 125ms - a
            /// deliberate compromise, not a comfortable number. Wanting SLOW and SMOOTH at once
            /// is a request for more frames, which is what the rejected 60-frame sheet was for
            /// - see FrameCount.
            ///
            /// CHANGE THE SHEET AND THIS HAS TO MOVE WITH IT: a 60-frame sheet would need 17
            /// for this same tempo, and dropping one in without touching this would slow the
            /// fire to less than half by accident.</summary>
            public static float Fps = 8f;

            /// <summary>Flame height as a fraction of the arena's SHORT side, at the first
            /// overtime level and at full heat. Large: this is a ring of fire, not a pilot
            /// light, and the first pass at a third of these numbers read as birthday candles.</summary>
            public static float HeightLow = 0.26f;

            public static float HeightHigh = 0.46f;

            /// <summary>Corners are the anchors of the ring, so they are the biggest.</summary>
            public static float CornerScale = 1.25f;

            /// <summary>The two BOTTOM corner flames are the hero element and are sized on their
            /// own terms - as a multiple of the arena's short side, at the first overtime level
            /// and at full heat. At 1.0 a flame stands from the bottom corner to the very top of
            /// the board.
            ///
            /// This is as large as the frame allows, not as large as asked. With its foot pinned
            /// to the corner at world y -2.35 and the camera ending at y 5, there are 7.35 units
            /// of screen above it: a "ten times bigger" flame would be 18 units and the view is
            /// only 10 tall, so anything past ~4x the previous size leaves the screen with its
            /// tip cut flat.
            ///
            /// THIS IS A HEIGHT, AND HEIGHT ALONE IS NOT SIZE. The sprite is scaled uniformly
            /// from it, so how BULKY the flame looks depends on how wide the artwork is inside
            /// its cell - and it changed with every sheet delivered: 0.167 cell-heights wide on
            /// the 8-frame art, 0.238 on this one, 0.270 on the 60-frame that was tried after.
            /// Matching HEIGHTS across a swap put a flame on screen 28% wider and 13% larger in
            /// area than the one it replaced, and it read as a size increase nobody asked for.
            /// Match drawn AREA instead whenever a new sheet lands.
            ///
            /// 1.19 is the ceiling that does not CLIP - the foot sits at world y -2.425, the
            /// view ends at 5, and the tallest frame fills 96% of its cell. Anything above it
            /// cuts frames flat against the top of the screen; at 1.50 nineteen of the
            /// twenty-eight were cut.</summary>
            public static float BottomCornerHeightLow = 0.62f;

            public static float BottomCornerHeightHigh = 0.86f;

            /// <summary>Whether the two TOP corners get a flame as well. OFF: the design
            /// settled on the two bottom corner flames being the entire effect. Next to flames
            /// the height of the board, anything the headroom above the arena can fit reads as
            /// a leftover rather than as part of the same fire.</summary>
            public static bool TopCorners = false;

            /// <summary>Flames per EDGE - not per world unit - at the first overtime level and
            /// at full heat. BOTH ZERO: the ring was tried at several densities and lost to two
            /// large flames. The machinery is left reachable rather than deleted because this
            /// went round several times; raise these and the sides and top come back, sparse and
            /// spaced off the corners, exactly as they were.</summary>
            public static float EdgeCountLow = 0f;

            public static float EdgeCountHigh = 0f;

            /// <summary>How far in from the corners an edge flame may be placed, as a fraction
            /// of the edge. Without it the first edge flame crowds the corner one.</summary>
            public static float EdgeInset = 0.22f;

            /// <summary>How much taller or shorter an individual flame may be than its
            /// neighbours. Without this the ring reads as one repeated stamp.</summary>
            public static float SizeJitter = 0.22f;

            /// <summary>Spread of per-flame playback speed, as a fraction. Small: past about
            /// 0.3 the slow ones visibly lag rather than just being out of step.</summary>
            public static float SpeedJitter = 0.22f;

            /// <summary>How far a SIDE flame is pushed out of the arena, in half-widths of its
            /// own sprite. Over 1 so the body clears the edge column rather than grazing it.</summary>
            public static float SideClearance = 1.15f;

            /// <summary>The bottom edge is the awkward one: a flame there burns UP, into the
            /// board. These two are the compromise. BottomScale keeps it smaller than the
            /// dramatic side flames, and BottomBed drops it below the rim by a fraction of its
            /// own height - not by all of it, because the art fills anywhere from 32% to 99% of
            /// its cell depending on the frame, and bedding by the full height strands every
            /// short frame in mid-air. At these values the shortest frame stops a quarter of a
            /// unit short of the rim and the tallest licks about three quarters of a cell over
            /// it, which is a flicker rather than an obstruction.</summary>
            public static float BottomScale = 0.60f;

            public static float BottomBed = 0.45f;

            /// <summary>Fraction of the room ABOVE the board a top flame may fill. The board
            /// sits high in the frame - its top edge is at world y 4.15 against a camera that
            /// ends at 5 - so there is under a unit of headroom and a top flame at the size the
            /// sides get would run off the screen with its tip cut flat. Nothing else can give:
            /// the board's position is fixed layout.</summary>
            public static float TopFill = 0.88f;

            /// <summary>Whether the little flames along the bottom edge are drawn at all. OFF:
            /// they were built, packed shoulder to shoulder and tuned, and the design came back
            /// to the two corner flames alone. Everything they need is still here and one flag
            /// away, sheet included.</summary>
            public static bool SmallRow = false;

            /// <summary>Height of the little BOTTOM-EDGE flames, as a fraction of the arena's
            /// short side, at the first overtime level and at full heat. They are meant to read
            /// as embers along the rim next to the corner flames, so they stay around a seventh
            /// of their height - close enough to belong to the same fire, far enough not to
            /// compete with it.</summary>
            public static float SmallHeightLow = 0.080f;

            public static float SmallHeightHigh = 0.108f;

            /// <summary>Spacing of the bottom row, as a multiple of a flame's typical drawn
            /// width. Under 1 so the row packs tight: at 1.0 the flames merely sat next to each
            /// other and read as a fence. The count is derived from this and the space available,
            /// never fixed, so it holds on a 5x5 arena and a 9x9 alike.</summary>
            public static float SmallSpacing = 0.82f;

            /// <summary>How much of a corner flame's width the bottom row keeps clear of, as a
            /// fraction. Well under 1: the corner flames are enormous and the little ones are
            /// not, so tucking the ends of the row against the foot of each corner flame reads
            /// as one fire. Reserving the corner flame's full width left an obvious hole at each
            /// end.</summary>
            public static float SmallCornerClearance = 0.45f;

            /// <summary>Sideways scatter of each little flame, as a fraction of the row's pitch.
            /// Evenly pitched identical sprites read as placed by hand however dense they are;
            /// this is what breaks the rhythm. Kept small - past about 0.25 they start colliding
            /// visibly rather than looking scattered.</summary>
            public static float SmallJitter = 0.15f;

            /// <summary>Multiplied into the art. White leaves the drawn colours alone; this is
            /// here for a boss or a deck that wants a different fire.</summary>
            public static Color Tint = Color.white;
        }

        // =================================================================== internals

        private const string SheetPath = "Art/Fx/flame_sheet";
        /// <summary>28 frames, laid out 7 across and 4 down. The slicer walks the grid in
        /// reading order - left to right, top row first - which is the order the cycle runs in.
        /// A single strip would be 3584px wide against the importer's 2048 cap, hence the grid.
        ///
        /// A 60-FRAME SHEET WAS TRIED AND REJECTED. It animated more smoothly, but its frames
        /// were half the height (241px against 459), and the flame is drawn on screen at over
        /// 500px: 1.2x of upscaling became 2.2x, and this art is flat fills with a black
        /// OUTLINE, which is exactly what softens first. More frames is not worth blurred
        /// linework here.</summary>
        private const int FrameCount = 28;

        private const int FrameColumns = 7;

        private const int FrameRows = 4;

        /// <summary>Where the drawn flame's LOWEST pixel sits inside its cell, as a fraction of
        /// the cell height. The repack plants every frame exactly 4px above a 480px cell floor,
        /// so the sprite pivot goes here rather than at the floor - otherwise "the foot is on
        /// the corner" is off by that much.</summary>
        private const float FootPad = 4f / 480f;

        // ---- the little flames along the bottom edge, on their own sheet ----
        private const string SmallSheetPath = "Art/Fx/flame_small_sheet";
        private const int SmallFrameCount = 20;
        private const int SmallFrameColumns = 10;
        private const int SmallFrameRows = 2;
        private const float SmallFootPad = 4f / 152f;

        /// <summary>The widest BIG frame's drawn width as a fraction of its cell height. Needed
        /// to know how far a corner flame reaches along the bottom edge, since it is planted ON
        /// the corner and half of it lies inside.</summary>
        private const float ContentWidth = 114f / 480f;

        /// <summary>The TYPICAL drawn width of a small frame as a fraction of its cell height -
        /// the average across the sheet, not the maximum. The frames run 49px to 73px wide, so
        /// pitching on the widest one leaves most of the row standing in gaps and the whole thing
        /// reads as separate objects laid out by hand. Pitching on the average lets the narrow
        /// frames breathe and the widest few overlap slightly, which is what a row of fire does.
        /// Spacing has to follow the ART, not the cell: the cell is wider still, to give the tips
        /// room.</summary>
        private const float SmallContentWidth = 66f / 152f;

        /// <summary>Level at which heat stops climbing at full rate (40% after it).</summary>
        private const int FullHeatLevel = 6;

        /// <summary>Above the board's cells and its ambient wash (0-3), below the hand.</summary>
        private const int SortingOrder = 4;

        /// <summary>Where a flame stands. The four corners are their own sites because they
        /// need BOTH a sideways nudge and (at the bottom) a drop, and because they are placed
        /// unconditionally while the edges are not.</summary>
        private enum Site
        {
            Bottom,
            Right,
            Top,
            Left,
            CornerBottomLeft,
            CornerBottomRight,
            CornerTopRight,
            CornerTopLeft
        }

        private struct Flame
        {
            public SpriteRenderer Renderer;

            /// <summary>Frame it starts on, so the ring is never in step.</summary>
            public float Phase;

            public float Speed;

            /// <summary>True for the little flames along the bottom edge, which run off their
            /// own sheet at their own frame count.</summary>
            public bool Small;
        }

        private static Sprite[] frames;
        private static Sprite[] smallFrames;
        private static bool sheetMissing;
        private static bool smallSheetMissing;

        /// <summary>Width of a frame as a fraction of its height, taken from the sheet so the
        /// side clearance follows the art rather than a number copied out of it.</summary>
        private static float frameAspect = 0.2f;

        /// <summary>The embers thrown off the fire. Owned here rather than by the controller
        /// because only this knows where the flames ended up.</summary>
        private FlameEmbersView embers;

        /// <summary>Reused so telling the embers where the fire is costs no allocation.</summary>
        private readonly List<Vector2> emberSources = new List<Vector2>();
        private Flame[] flames = new Flame[0];
        private int live;
        private int level;
        private Rect area;
        private float clock;
        private Camera cam;
        private bool dirty;

        /// <summary>The eight frames, sliced from the sheet once and shared. Built with the
        /// pivot at the BOTTOM CENTRE and pixelsPerUnit equal to the cell height, so a sprite is
        /// exactly one world unit tall at scale 1 and stands on its own foot - which makes the
        /// transform's scale the flame's height in world units, with no arithmetic anywhere
        /// else.</summary>
        private static Sprite[] Frames
        {
            get
            {
                if (frames == null && !sheetMissing)
                {
                    frames = Load(SheetPath, FrameCount, FrameColumns, FrameRows, FootPad,
                        out frameAspect, ref sheetMissing);
                }
                return frames;
            }
        }

        /// <summary>The little bottom-edge flames. A separate sheet with its own frame count, so
        /// it can be redrawn or replaced without touching the corner fire.</summary>
        private static Sprite[] SmallFrames
        {
            get
            {
                if (smallFrames == null && !smallSheetMissing)
                {
                    float ignored;
                    smallFrames = Load(SmallSheetPath, SmallFrameCount, SmallFrameColumns,
                        SmallFrameRows, SmallFootPad, out ignored, ref smallSheetMissing);
                }
                return smallFrames;
            }
        }

        /// <summary>Slices a grid sheet in READING ORDER - left to right, top row first, which is
        /// the order every one of these cycles runs in. The pivot goes at the bottom centre and
        /// pixelsPerUnit at the cell height, so a sprite is exactly one world unit tall at scale
        /// 1 and stands on its own foot: that makes the transform's scale the flame's height in
        /// world units, with no arithmetic anywhere else.</summary>
        private static Sprite[] Load(string path, int count, int cols, int rows, float footPad,
            out float aspect, ref bool missing)
        {
            aspect = 1f;
            var sheet = Resources.Load<Texture2D>(path);
            if (sheet == null)
            {
                // Degrade rather than render nothing, like the block tiles do: a stripped build
                // loses the fire, it does not throw every frame of overtime.
                missing = true;
                Debug.LogWarning("[block_bonk] Flame sheet missing: Resources/" + path);
                return null;
            }
            int cellW = sheet.width / cols;
            int cellH = sheet.height / rows;
            aspect = cellW / (float)cellH;
            var sliced = new Sprite[count];
            for (int i = 0; i < count; i++)
            {
                int col = i % cols;
                int row = i / cols;
                // Texture space counts y from the BOTTOM while the sheet reads from the top, so
                // the first row of frames is the last row of texels.
                sliced[i] = Sprite.Create(sheet,
                    new Rect(col * cellW, (rows - 1 - row) * cellH, cellW, cellH),
                    new Vector2(0.5f, footPad), cellH);
            }
            return sliced;
        }

        public void SetState(int overtimeLevel, Rect boardArea)
        {
            int newLevel = Mathf.Clamp(overtimeLevel, 0, MaxLevel);
            if (newLevel != level || boardArea != area)
            {
                dirty = true;
            }
            level = newLevel;
            area = boardArea;
        }

        private void Update()
        {
            if (dirty)
            {
                Rebuild();
                dirty = false;
            }
            if (live == 0)
            {
                return;
            }
            Sprite[] big = Frames;
            Sprite[] small = SmallFrames;
            clock += Time.deltaTime;
            for (int i = 0; i < live; i++)
            {
                Flame fl = flames[i];
                Sprite[] set = fl.Small ? small : big;
                if (set == null)
                {
                    continue;
                }
                // The two sheets have different frame counts, so at one Fps they cycle at
                // different lengths. That is left alone: the bottom row breathing out of step
                // with the corners is what stops the whole thing pulsing as one animation.
                int n = fl.Small ? SmallFrameCount : FrameCount;
                float t = clock * Style.Fps * fl.Speed + fl.Phase;
                fl.Renderer.sprite = set[(int)Mathf.Repeat(t, n)];
            }
        }

        // ------------------------------------------------------------------ placement

        /// <summary>Heat, 0..1. Full rate to FullHeatLevel and 40% after it, so the twelfth
        /// level still reads deeper than the sixth without being twice it.</summary>
        private float Heat
        {
            get
            {
                if (level <= 0)
                {
                    return 0f;
                }
                float eased = level <= FullHeatLevel
                    ? level
                    : FullHeatLevel + (level - FullHeatLevel) * 0.4f;
                return Mathf.Clamp01(eased / FullHeatLevel);
            }
        }

        private void Rebuild()
        {
            if (level <= 0 || area.width <= 0f || area.height <= 0f || Frames == null)
            {
                HideFrom(0);
                Embers.SetState(null, 0f, 0f, 0f);
                return;
            }

            float heat = Heat;
            float baseHeight = Mathf.Min(area.width, area.height)
                * Mathf.Lerp(Style.HeightLow, Style.HeightHigh, heat);
            int perEdge = Mathf.Max(0, Mathf.RoundToInt(
                Mathf.Lerp(Style.EdgeCountLow, Style.EdgeCountHigh, heat)));

            float cornerHeight = Mathf.Min(area.width, area.height)
                * Mathf.Lerp(Style.BottomCornerHeightLow, Style.BottomCornerHeightHigh, heat);

            float smallHeight = Mathf.Min(area.width, area.height)
                * Mathf.Lerp(Style.SmallHeightLow, Style.SmallHeightHigh, heat);
            int smallCount = BottomRowCount(cornerHeight, smallHeight);

            EnsureCapacity(2 + (Style.TopCorners ? 2 : 0) + perEdge * 3 + smallCount);
            int n = 0;

            // The four corners first, and unconditionally. They are what makes the ring read as
            // a ring: an even walk around the perimeter puts flames wherever the arithmetic
            // lands, and a corner left dark reads as a gap rather than as spacing.
            // The VISIBLE corner, not the grid corner: WorldRect reports the cell area, while
            // the board's background plate is drawn BorderOverhang wider, so the dark edge the
            // player sees is half of that further out. Planting on the grid corner leaves the
            // foot hanging just above the arena, which is exactly how it looked.
            float lip = BoardView.BorderOverhang * 0.5f;
            Place(n++, new Vector2(area.xMin - lip, area.yMin - lip),
                Site.CornerBottomLeft, cornerHeight);
            Place(n++, new Vector2(area.xMax + lip, area.yMin - lip),
                Site.CornerBottomRight, cornerHeight);
            if (Style.TopCorners)
            {
                Place(n++, new Vector2(area.xMax, area.yMax), Site.CornerTopRight, baseHeight);
                Place(n++, new Vector2(area.xMin, area.yMax), Site.CornerTopLeft, baseHeight);
            }

            // The BOTTOM ROW: little flames packed shoulder to shoulder between the two corner
            // ones, filling the arena's bottom lip. Their count is derived, never fixed - see
            // BottomRowCount - so they stay touching whatever size the arena or they are.
            if (smallCount > 0)
            {
                float rowY = area.yMin - lip;
                float inset = cornerHeight * ContentWidth * Style.SmallCornerClearance * 0.5f;
                float x0 = area.xMin - lip + inset;
                float pitch = (area.xMax + lip - inset - x0) / smallCount;
                for (int k = 0; k < smallCount; k++)
                {
                    PlaceSmall(n++, new Vector2(x0 + pitch * (k + 0.5f), rowY),
                        smallHeight, pitch);
                }
            }

            // Then the edges, held clear of the corners and spaced evenly with a nudge.
            float span = 1f - 2f * Style.EdgeInset;
            // The bottom EDGE has no ring flames: down there the corner pair plus the little row
            // above are the fire, and a mid-sized flame between them belongs to neither.
            for (int e = 1; e < 4; e++)
            {
                for (int k = 0; k < perEdge; k++)
                {
                    float f = Style.EdgeInset + span * ((k + 0.5f) / perEdge)
                        + (Hash01(n * 7 + 3) - 0.5f) * span / perEdge * 0.7f;
                    Site site;
                    Vector2 pos;
                    switch (e)
                    {
                        case 0:
                            site = Site.Bottom;
                            pos = new Vector2(area.xMin + area.width * f, area.yMin);
                            break;
                        case 1:
                            site = Site.Right;
                            pos = new Vector2(area.xMax, area.yMin + area.height * f);
                            break;
                        case 2:
                            site = Site.Top;
                            pos = new Vector2(area.xMax - area.width * f, area.yMax);
                            break;
                        default:
                            site = Site.Left;
                            pos = new Vector2(area.xMin, area.yMax - area.height * f);
                            break;
                    }
                    Place(n++, pos, site, baseHeight);
                }
            }

            // The embers are thrown from the corner flames, so they are told where those ended
            // up rather than working it out again.
            emberSources.Clear();
            emberSources.Add(new Vector2(area.xMin - lip, area.yMin - lip));
            emberSources.Add(new Vector2(area.xMax + lip, area.yMin - lip));
            Embers.SetState(emberSources, cornerHeight * ContentWidth, cornerHeight, heat);

            HideFrom(n);
            live = n;
        }

        /// <summary>The ember layer, made on first use. Its own object so its sprites are not
        /// mixed in with the flame pool.</summary>
        private FlameEmbersView Embers
        {
            get
            {
                if (embers == null)
                {
                    var go = new GameObject("Embers");
                    go.transform.SetParent(transform, false);
                    embers = go.AddComponent<FlameEmbersView>();
                }
                return embers;
            }
        }

        /// <summary>Sizes one flame for its site, offsets it clear of the board and writes it.
        /// Everything site-specific is here, so Rebuild only has to say WHERE.</summary>
        private void Place(int index, Vector2 pos, Site site, float baseHeight)
        {
            float r1 = Hash01(index * 3 + 1);
            float r2 = Hash01(index * 3 + 2);
            float r3 = Hash01(index * 3 + 3);

            bool corner = site >= Site.CornerBottomLeft;
            bool bottom = site == Site.Bottom
                || site == Site.CornerBottomLeft || site == Site.CornerBottomRight;
            bool leftSide = site == Site.Left
                || site == Site.CornerBottomLeft || site == Site.CornerTopLeft;
            bool rightSide = site == Site.Right
                || site == Site.CornerBottomRight || site == Site.CornerTopRight;
            bool top = site == Site.Top
                || site == Site.CornerTopLeft || site == Site.CornerTopRight;

            // The bottom corners are passed their own height already and take no scaling: they
            // are sized as the hero element, not as a corner-sized version of the ring.
            bool bottomCorner = site == Site.CornerBottomLeft || site == Site.CornerBottomRight;
            float scale = bottomCorner ? 1f : (corner ? Style.CornerScale : 1f);
            if (bottom && !bottomCorner)
            {
                scale *= Style.BottomScale;
            }
            float jitter = bottomCorner ? Style.SizeJitter * 0.45f : Style.SizeJitter;
            float size = baseHeight * scale * (1f + (r1 - 0.5f) * 2f * jitter);
            if (top)
            {
                // Whatever room is actually left above the board, asked of the camera rather
                // than assumed, so this still holds if the arena or the framing ever moves.
                size = Mathf.Min(size, Headroom * Style.TopFill);
            }

            // A BOTTOM CORNER stands exactly on its corner: no bed, no sideways nudge. The foot
            // lands on the corner point itself, which is the whole look, and the pivot already
            // accounts for the padding under the drawing (see FootPad). It costs some occlusion
            // of the edge column - a flame planted ON a corner is half over the board by
            // definition - and that is accepted rather than worked around.
            if (!bottomCorner)
            {
                float halfWidth = size * frameAspect * 0.5f * Style.SideClearance;
                if (bottom)
                {
                    pos.y -= size * Style.BottomBed;
                }
                if (leftSide)
                {
                    pos.x -= halfWidth;
                }
                if (rightSide)
                {
                    pos.x += halfWidth;
                }
            }

            Flame fl = flames[index];
            fl.Small = false;
            fl.Phase = r2 * FrameCount;
            fl.Speed = 1f + (r3 - 0.5f) * 2f * Style.SpeedJitter;
            fl.Renderer.transform.localPosition = pos;
            fl.Renderer.transform.localScale = new Vector3(size, size, 1f);
            fl.Renderer.color = Style.Tint;
            fl.Renderer.enabled = true;
            flames[index] = fl;
        }

        /// <summary>How many little flames fit along the bottom lip between the two corner
        /// flames, touching but never overlapping. Derived rather than fixed: the arena is 5x5
        /// to 9x9 across a run and both flame sizes ride the overtime level, so any constant
        /// here would crowd one board and leave gaps on another.
        ///
        /// The span starts where the corner flames stop reaching. They are planted ON the
        /// corners, so half of each one's drawn width lies inside the edge.</summary>
        private int BottomRowCount(float cornerHeight, float smallHeight)
        {
            if (!Style.SmallRow || SmallFrames == null || smallHeight <= 0f)
            {
                return 0;
            }
            float lip = BoardView.BorderOverhang * 0.5f;
            float span = (area.width + 2f * lip)
                - cornerHeight * ContentWidth * Style.SmallCornerClearance;
            float pitch = smallHeight * SmallContentWidth * Style.SmallSpacing;
            return pitch <= 0f ? 0 : Mathf.Max(0, Mathf.FloorToInt(span / pitch));
        }

        /// <summary>One little bottom-edge flame. Planted foot-down on the lip like the corner
        /// pair, with only a touch of size variation - they read as a ROW, and a row whose
        /// members disagree too much about their height reads as a mistake rather than as
        /// flicker. Their PHASE is spread across the whole cycle instead, which is where the
        /// life comes from.</summary>
        private void PlaceSmall(int index, Vector2 pos, float height, float pitch)
        {
            float r1 = Hash01(index * 3 + 1);
            float r2 = Hash01(index * 3 + 2);
            float r3 = Hash01(index * 3 + 3);
            pos.x += (Hash01(index * 7 + 5) - 0.5f) * 2f * Style.SmallJitter * pitch;

            Flame fl = flames[index];
            fl.Small = true;
            fl.Phase = r2 * SmallFrameCount;
            fl.Speed = 1f + (r3 - 0.5f) * 2f * Style.SpeedJitter;
            float size = height * (1f + (r1 - 0.5f) * 2f * Style.SizeJitter * 0.4f);
            fl.Renderer.transform.localPosition = pos;
            fl.Renderer.transform.localScale = new Vector3(size, size, 1f);
            fl.Renderer.color = Style.Tint;
            fl.Renderer.enabled = true;
            flames[index] = fl;
        }
        /// <summary>World units between the top of the arena and the top of the view.</summary>
        private float Headroom
        {
            get
            {
                if (cam == null)
                {
                    cam = Camera.main;
                }
                if (cam == null)
                {
                    return area.height;   // no camera to ask: do not clamp
                }
                return Mathf.Max(0.01f, cam.orthographicSize + cam.transform.position.y - area.yMax);
            }
        }

        private void EnsureCapacity(int want)
        {
            if (flames.Length >= want)
            {
                return;
            }
            var grown = new Flame[want];
            System.Array.Copy(flames, grown, flames.Length);
            for (int i = flames.Length; i < want; i++)
            {
                var go = new GameObject("Flame" + i);
                go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sortingOrder = SortingOrder;
                grown[i] = new Flame { Renderer = sr };
            }
            flames = grown;
        }

        /// <summary>Turns off every flame from `from` on. Renderers are kept for reuse - a round
        /// can cross in and out of overtime, and rebuilding the pool each time would allocate on
        /// exactly the frames that are already busy.</summary>
        private void HideFrom(int from)
        {
            for (int i = from; i < flames.Length; i++)
            {
                if (flames[i].Renderer != null)
                {
                    flames[i].Renderer.enabled = false;
                }
            }
            if (from == 0)
            {
                live = 0;
            }
        }

        /// <summary>Stable 0..1 from an index. Not Random: the same flame must come back the
        /// same on every rebuild, or deepening overtime would reshuffle the whole ring.</summary>
        private static float Hash01(int n)
        {
            unchecked
            {
                int h = n * 374761393 + 668265263;
                h = (h ^ (h >> 13)) * 1274126177;
                h ^= h >> 16;
                return ((h & 0x7fffffff) % 100000) / 100000f;
            }
        }
    }
}
