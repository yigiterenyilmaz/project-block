// PURPOSE: "Karantina"'s sealed zones. Not cells painted yellow - a containment field that closes
// over a REGION of the board and stays there.
//
// ONE MASK, NOT A GRID OF RECTANGLES. Every quarantined cell goes into a single occupancy mask,
// and everything drawn is derived from the SIGNED DISTANCE to that mask's boundary. That one
// decision is what makes the field read as a sealed area instead of a row of squares: two touching
// cells have no boundary between them to draw, so the shared border cannot appear even in
// principle, and a row crossing a column becomes one cross-shaped field with a single outer
// contour rather than two overlays stacked at the intersection.
//
// THE SEAL BLOOMS OUT OF EACH NEW CELL. A newly quarantined cell is not switched on. Each pixel
// carries the time its own seal arrives, measured outward from the centre of the cell it belongs
// to, so the film spreads from the middle of every new square instead of sweeping in from a board
// edge. That changed with the mechanic: the boss no longer takes the outermost clean LINE and work
// inward - it lays a scattered patch of cells and relays it somewhere else every few turns, and
// there is no edge such a patch could be said to come from. The wave is still a bright front with
// membrane settling behind it.
//
// THE ZONE MOVES, SO CELLS LEAVE AS WELL AS ARRIVE. Anything no longer in the set is simply gone
// from the mask on the next rebuild; only ARRIVALS get a front. A relaying therefore reads as the
// old patch lifting and a new one sealing itself, which is exactly what happened.
//
// IT IS A FILM THAT COVERS, NOT A TINT THAT DARKENS. This was got wrong twice. A near-black layer
// at partial alpha leaves the whole surface squeezed between the board and black - measured, that
// is EIGHT levels of grey for every bit of texture to live in, which is why mottling and grain and
// scratches were all in the code and none of them were on the screen. So the film is opaque enough
// to actually replace the surface, and its colour is a dirty olive-grey near the board's own
// value. The texture then modulates that COLOUR rather than the alpha, and has roughly three times
// the range to be seen in.
//
// THE DEADNESS COMES FROM THE MATERIAL, NOT FROM DARKNESS. The blocks are drained at source and the
// surface is matte, filthy and uneven; that is what says the area is finished. Painting it black
// only says somebody turned a light off. Amber survives as an accent: a thin seam inside the rim
// and the occasional flicker, never as the surface.
//
// DESATURATION HAPPENS AT SOURCE, NOT HERE. Alpha blending can only mix toward a colour; it cannot
// drain one. So the blocks inside a zone are drained where they are painted, in BoardView, and this
// film only darkens and dirties on top. A block therefore keeps its identity - you can still see
// WHICH block it is - while plainly having had the life pulled out of it.
//
// THE SURFACE IS A MATERIAL, NOT AN OPACITY. A single uniform alpha over a region is a rectangle
// however dark it is, so the film carries four things at once: broad smoky mottling that drifts
// almost too slowly to notice, fine contamination grain, a handful of dry scratches, and soot
// gathering where the perimeter TURNS. Everything but the mottling is BAKED once when the shape
// changes - scratches and soot do not move, and paying for them every frame over a board-sized
// region would be the one thing that makes this expensive.
//
// EACH REGION WEARS DIFFERENTLY. The bake only rewrites pixels that were not already sealed, with a
// fresh seed each time, so a line added later carries its own grain and its own scratches. The zone
// grows the way a stain does rather than being redrawn as one flat sheet.
//
// IT IS NOT THE INFECTION. No filaments, no core, no spores, nothing alive. The infection is green,
// organic, lit from within and cell-shaped. This is dark, matte, dead, sealed and area-shaped.

using System.Collections.Generic;
using ProjectBlock.Core;
using UnityEngine;

namespace ProjectBlock.View
{
    /// <summary>The containment field over "Karantina"'s sealed lines.</summary>
    public sealed class QuarantineFieldView : MonoBehaviour
    {
        // =================================================================== TUNING
        public static class Style
        {
            // ------------------------------------------------------------------ dead zone film
            /// <summary>How heavily the film lies over an EMPTY cell - and, unavoidably, the one
            /// number that trades the film's own texture against the board showing THROUGH it.
            /// Whatever is left of the board under the film is exactly 1 minus this, so at 0.58 the
            /// slot insets and the grid keep about forty per cent of their contrast and the player
            /// can still see which cells are sealed. It was 0.88, which left twelve per cent and
            /// turned the sealed lines into one solid band. What that costs the film is paid back
            /// by the texture below being pushed harder, not by putting the opacity back.</summary>
            public static float DeadZoneOpacity = 0.58f;

            /// <summary>Read-only companion to the above: how much of the board's own geometry
            /// survives inside a zone. Named because it is what the number is actually FOR.</summary>
            public static float GridVisibilityInsideQuarantine
            {
                get { return 1f - DeadZoneOpacity; }
            }

            /// <summary>And over a cell with a block in it. Lower - the block has already been
            /// drained where it was painted, and burying it as well would cost the player the one
            /// thing they still need to read: WHICH block that is.</summary>
            public static float FilledCellOverlayStrength = 0.62f;

            public static float EmptyCellOverlayStrength = 1f;

            /// <summary>The film itself: a dirty olive-grey sitting near the board's OWN value,
            /// not near black. That is deliberate and it is the whole fix - a film this colour has
            /// room above and below it for texture to be seen, where a near-black one has none.
            /// It still reads dead, because everything in the zone is drained and matte around
            /// it. Raised again alongside the opacity drop: with less of the film reaching the
            /// screen, its colour has to carry a wider swing for the same texture to be seen.</summary>
            public static Color FilmColor = new Color(0.175f, 0.168f, 0.133f);

            /// <summary>The rim. DARKER than the film, not brighter - the border of a sealed area
            /// is where the light goes, not where it comes from.</summary>
            public static Color SealBandColor = new Color(0.010f, 0.011f, 0.009f);

            /// <summary>The one place amber is allowed to live: a thin, low-saturation warning
            /// line just inside the seal band. Sick, not golden.</summary>
            public static Color WarningColor = new Color(0.46f, 0.37f, 0.19f);

            /// <summary>The rare diagnostic pass over a dead zone. Pale, dirty amber - a system
            /// still checking on something it has already given up on.</summary>
            public static Color ScanColor = new Color(0.44f, 0.38f, 0.24f);

            // ------------------------------------------------------------------ surface
            /// <summary>Broad smoky blotching, the layer that stops the region being uniform.
            /// Two octaves: one about two cells across, one about half that. It is the ONLY part
            /// of the surface that moves, and it moves slowly enough that the player should never
            /// consciously notice an animation.</summary>
            /// <summary>All the surface strengths below are now fractions of the FILM COLOUR, not
            /// additions to alpha. A tenth here is a tenth of the surface's brightness, which is
            /// visible; a tenth of alpha over near-black was not.</summary>
            public static float MottlingStrength = 0.62f;

            /// <summary>Below one this pushes the mottling toward its extremes, so the surface has
            /// distinct smoky areas rather than an even wobble around the middle. Contrast, not
            /// amplitude - which is what makes it readable without making it louder.</summary>
            public static float MottlingContrast = 0.66f;

            public static float MottlingScale = 0.48f;

            public static float MottlingSpeed = 0.030f;

            /// <summary>Patches where the life was pulled out harder than elsewhere. Very low
            /// frequency and one-sided - it only ever makes an area DEADER, never livelier.</summary>
            public static float PatchStrength = 0.32f;

            public static float PatchScale = 0.21f;

            /// <summary>A second, tighter octave of staining between the broad patches and the
            /// grain. Without something at this scale the surface has big shapes and fine dust and
            /// a conspicuous gap in the middle where a real dirty material has most of its
            /// character.</summary>
            public static float StainStrength = 0.34f;

            public static float StainScale = 0.78f;

            /// <summary>A third layer above the stains: a few actual DIRTY PATCHES rather than
            /// continuous variation. Thresholded, so most of the surface carries none at all and
            /// the ones that exist have somewhere to end - which is what a stain does and what
            /// noise, however many octaves of it, does not.</summary>
            public static float BlotchStrength = 0.30f;

            public static float BlotchScale = 1.45f;

            public static float BlotchCoverage = 0.30f;

            /// <summary>Fine dirt. Shaped so only the top of the noise range produces anything,
            /// which is the difference between specks of contamination and television static.</summary>
            public static float GrainStrength = 0.38f;

            /// <summary>Coarser than it was. Fine noise at this size reads as pixel static; a
            /// bigger speck reads as dust on a surface, which is the thing being drawn.</summary>
            public static float GrainScale = 3.4f;

            public static float GrainSparsity = 0.55f;

            /// <summary>Dry scratches and crease marks in the film, per ten cells of region. They
            /// catch a little light rather than darkening, which is what makes them read as
            /// damage to a surface instead of more dirt on it.</summary>
            public static float ScratchDensity = 3.2f;

            /// <summary>More of them and each one FAINTER. A scratch the player can point at is
            /// a scratch that has become the subject; the target is a surface that reads as old,
            /// not a surface with scratches on it.</summary>
            public static float ScratchOpacity = 0.46f;

            public static float ScratchLengthMin = 0.35f;

            public static float ScratchLengthMax = 1.30f;

            /// <summary>Soot gathering at the perimeter, and how much more of it collects where
            /// the perimeter TURNS. Corners are where a real seal would be dirtiest.</summary>
            public static float SootEdgeStrength = 0.48f;

            public static float SootCornerBoost = 2.8f;

            // ------------------------------------------------------------------ seal edge
            /// <summary>In CELLS, and THIN on purpose. A quarantine line is one cell wide, so a
            /// seal reaching a third of a cell in from each side eats seventy per cent of it and
            /// the whole line stops being a surface and becomes a tube. At these numbers the seal
            /// takes about an eighth of a cell and the other three quarters is material - which is
            /// the right hierarchy: the surface is the effect, the seal only closes it.</summary>
            public static float OuterSealWidth = 0.030f;

            public static float SealFalloff = 0.085f;

            public static float OuterSealDarkness = 0.68f;

            /// <summary>How much the seal's own darkness wanders along the perimeter. Without this
            /// it is an even stroke at an even weight, which is the one thing that reads as a UI
            /// frame no matter how thin it gets - some stretches have to be nearly buried.</summary>
            public static float SealBreakUp = 0.55f;

            public static float SealBreakUpScale = 2.2f;

            /// <summary>A much wider, much weaker darkening inside the seal. This is the light
            /// absorption that seats the area into the board; it is not allowed to be findable as
            /// an edge, which is why it is four times the seal's width and a third its strength.</summary>
            public static float InnerShadowStrength = 0.20f;

            public static float InnerShadowWidth = 0.34f;

            /// <summary>How far the contour is rounded, in cells - the outside corners and the
            /// inside ones of an L separately, because they want different amounts. Small: on a
            /// one-cell line anything larger turns the whole line into a capsule.</summary>
            public static float CornerRadiusOuter = 0.085f;

            public static float CornerRadiusInner = 0.11f;

            /// <summary>What is left of a hazard marking, not a hazard marking. Weak, and - the
            /// important part - BROKEN: it survives on some stretches of the perimeter and has
            /// worn off others. A seam that runs the whole way round is a UI stroke.</summary>
            public static float AmberResidueOpacity = 0.42f;

            public static float AmberResidueOffset = 0.075f;

            /// <summary>Roughly what fraction of the perimeter still carries any.</summary>
            /// <summary>Fewer stretches, each more legible - old paint survives in patches, not
            /// evenly and faintly all the way round.</summary>
            public static float AmberResidueCoverage = 0.38f;

            public static float AmberResidueScale = 1.7f;

            public static float EdgeShadow = 0.26f;

            public static float EdgeShadowWidth = 0.12f;

            /// <summary>How sharply the membrane itself stops at the boundary.</summary>
            public static float EdgeSoftness = 0.055f;

            // ------------------------------------------------------------------ sealing
            /// <summary>The beat before a new seal starts to close, so an arrival is a beat of
            /// its own rather than a jump. Long enough to be noticed, too short to be a wait.</summary>
            public static float WarningDuration = 0.17f;

            /// <summary>How long the front takes to cross the whole board.</summary>
            public static float SweepDuration = 0.42f;

            /// <summary>How much harder the front presses than the film that settles behind it.
            /// The wave is the DARKEST moment, not the brightest: the area is being killed, and
            /// what passes over it should look like the light being taken rather than given.</summary>
            public static float SweepDarkenStrength = 0.55f;

            /// <summary>The one glint of amber on the front, so the wave is findable at all against
            /// a dark board. Tiny by design.</summary>
            public static float SweepGlint = 0.13f;

            /// <summary>How wide the bright front is, in seconds of its own travel.</summary>
            public static float SweepWaveWidth = 0.075f;

            /// <summary>How long a point takes to settle into membrane once the front has passed.</summary>
            public static float SweepSettle = 0.13f;

            /// <summary>Two lines sealed on the same tick start slightly apart, so the board reads
            /// them as two events rather than one wall.</summary>
            public static float SweepStagger = 0.09f;

            /// <summary>The film pressing onto the board once the front has passed, then easing
            /// back. Small: this is a vacuum seating itself, not a scale animation.</summary>
            public static float FilmSettleDuration = 0.28f;

            public static float FilmSettlePress = 0.22f;

            public static float LockPulseDuration = 0.15f;

            public static float LockPulseStrength = 0.45f;

            /// <summary>How long a border that has just become INTERIOR takes to dissolve. This is
            /// what merging looks like: the old contour fades out where the new field swallowed
            /// it, instead of vanishing on the frame the topology changed.</summary>
            public static float MergeDissolve = 0.30f;

            // ------------------------------------------------------------------ idle flicker
            /// <summary>A short stretch of the seam catching, briefly, every few seconds - and a
            /// different stretch each time. Never the whole perimeter at once: an old containment
            /// system still barely working, not an alarm.</summary>
            public static float FlickerEveryMin = 4f;

            public static float FlickerEveryMax = 8f;

            public static float FlickerDuration = 0.09f;

            public static float FlickerRadius = 1.1f;

            public static float WarningSeamFlickerStrength = 0.55f;

            // ------------------------------------------------------------------ motes
            /// <summary>One suspended speck per this many quarantined cells. Sparse by design.</summary>
            public static float MoteCellsEach = 7f;

            public static float MoteSizeMin = 0.045f;

            public static float MoteSizeMax = 0.090f;

            public static float MoteSpeed = 0.085f;

            public static float MoteOpacity = 0.24f;

            /// <summary>Soot, not sparks: a shade lighter than the film so it is just findable,
            /// and no brighter than the board it drifts over.</summary>
            public static Color MoteColor = new Color(0.20f, 0.195f, 0.172f);

            // ------------------------------------------------------------------ reaction
            /// <summary>What a cube exploding inside the zone does to the membrane over it. The
            /// mechanic charges the player for that cube; this is the field noticing.</summary>
            public static float ReactionStrength = 0.85f;

            public static float ReactionDuration = 0.16f;

            public static float ReactionRadius = 0.85f;

            public static Color ReactionColor = new Color(0.26f, 0.15f, 0.03f);
        }

        /// <summary>Above the cubes and the ambient wash, under everything that explodes. The
        /// field is a layer over the board, not a thing on the board.</summary>
        private const int FieldOrder = 10;

        private const int MoteOrder = 11;

        /// <summary>Texture pixels per board cell. The whole field is one texture, so this is the
        /// only resolution knob; 14 is enough for a soft contour on an 11x11 board.</summary>
        private const int PixelsPerCell = 14;

        /// <summary>Cells of texture beyond the board, so the outer shadow has somewhere to go.</summary>
        private const float Padding = 0.6f;

        /// <summary>Repaints per second while nothing is sealing. The haze and the scan are slow
        /// enough that they do not need every frame, and the field can cover a whole board.</summary>
        private const float IdleRepaintHz = 30f;

        /// <summary>How far past a new line's own cells its reveal reaches, in pixels - enough
        /// to cover the outer shadow, which lives outside the field.</summary>
        private const int RevealMargin = 5;

        private const int LutSize = 256;

        /// <summary>The distance range the edge lookup covers, in cells.</summary>
        private const float LutRange = 0.45f;

        // =================================================================== state

        /// <summary>The quarantined cells in ABSOLUTE board coordinates - the whole state this
        /// view is driven by. Packed into a set as well, because the mask asks "is this cell in
        /// the zone" once per cell per rebuild.</summary>
        private readonly List<GridPos> cells = new List<GridPos>();

        private readonly HashSet<int> cellKeys = new HashSet<int>();

        private SpriteRenderer field;

        private Texture2D tex;

        private Color32[] pixels;

        private int texW;

        private int texH;

        /// <summary>Signed distance to the region boundary, in CELLS, positive inside.</summary>
        private float[] sdf = new float[0];

        /// <summary>The distance field as it was before the last change, for dissolving borders
        /// that have just become interior.</summary>
        private float[] prevSdf = new float[0];

        /// <summary>When each pixel's own seal arrives, in seconds on <see cref="clock"/>.
        /// Negative means it was already sealed before this change.</summary>
        private float[] revealAt = new float[0];

        /// <summary>Per pixel, how much field this cell wants - filled cells want less. Sampled
        /// bilinearly from the cell grid so it never has an edge.</summary>
        private float[] cellStrength = new float[0];

        /// <summary>The baked part of the surface: grain, dead patches, scratches and soot, as a
        /// signed addition to opacity. Rewritten only where the region GREW, so what was already
        /// sealed keeps the wear it had.</summary>
        private float[] surface = new float[0];

        /// <summary>Points on the perimeter, for the seam flicker to pick from.</summary>
        private readonly List<int> seamPixels = new List<int>();

        private int surfaceSeed;

        private float clock;

        private float mergeAt = -1f;

        private float lockAt = -1f;

        private float sealEndsAt = -1f;

        private float repaintedAt = -1f;

        private float nextScanAt;

        private float scanStartedAt = -1f;

        /// <summary>Where on the seam the current flicker is, in cells from the texture origin.</summary>
        private float flickerX;

        private float flickerY;

        // board geometry
        private int minX;

        private int minY;

        private int width;

        private int height;

        private float cellSize = 1f;

        private Vector2 origin;      // world position of the texture's bottom-left corner

        private bool built;

        private float spritePpu;

        // =================================================================== driving it

        /// <summary>
        /// Hands the field the boss's sealed cells. Works out for itself which of them are NEW -
        /// those are the ones that get a seal front; the rest are already film, and anything that
        /// has LEFT the set simply stops being drawn.
        /// </summary>
        public void SetCells(IReadOnlyList<GridPos> newCells, GameBoard board,
            System.Func<GridPos, Vector2> toWorld, float cell,
            System.Func<GridPos, bool> occupied)
        {
            if (board == null || cell <= 0f || newCells == null || newCells.Count == 0)
            {
                Clear();
                return;
            }

            bool boardChanged = board.MinX != minX || board.MinY != minY
                || board.Width != width || board.Height != height
                || !Mathf.Approximately(cell, cellSize);
            minX = board.MinX;
            minY = board.MinY;
            width = board.Width;
            height = board.Height;
            cellSize = cell;

            var added = new List<GridPos>();
            for (int i = 0; i < newCells.Count; i++)
            {
                if (!cellKeys.Contains(Key(newCells[i])))
                {
                    added.Add(newCells[i]);
                }
            }
            bool fresh = !built || boardChanged;

            cells.Clear();
            cellKeys.Clear();
            for (int i = 0; i < newCells.Count; i++)
            {
                cells.Add(newCells[i]);
                cellKeys.Add(Key(newCells[i]));
            }

            EnsureTexture(toWorld);
            BuildDistance(fresh);
            BuildStrength(occupied);
            BuildReveal(added, fresh);
            RebuildMotes();
            built = true;
            Paint();
        }

        /// <summary>A cell's identity in the key set. Board coordinates are small and may be
        /// negative, so they are biased into a positive range before being packed.</summary>
        private static int Key(GridPos cell)
        {
            return (cell.X + 512) * 4096 + (cell.Y + 512);
        }

        public void Clear()
        {
            cells.Clear();
            cellKeys.Clear();
            built = false;
            if (field != null)
            {
                field.enabled = false;
            }
            for (int i = 0; i < motes.Length; i++)
            {
                if (motes[i].R != null)
                {
                    motes[i].R.enabled = false;
                }
                motes[i].Live = false;
            }
        }

        /// <summary>A cube exploded inside the zone: the membrane over it darkens and draws in for
        /// a moment. Purely a reaction - nothing here decides anything.</summary>
        public void PlayReaction(Vector2 world)
        {
            if (!built)
            {
                return;
            }
            for (int i = 0; i < reactions.Length; i++)
            {
                if (reactions[i].At < 0f || clock - reactions[i].At > Style.ReactionDuration)
                {
                    reactions[i].At = clock;
                    reactions[i].Where = world;
                    return;
                }
            }
            reactions[0].At = clock;
            reactions[0].Where = world;
        }

        private struct Reaction
        {
            public float At;
            public Vector2 Where;
        }

        private Reaction[] reactions = new Reaction[]
        {
            new Reaction { At = -1f }, new Reaction { At = -1f },
            new Reaction { At = -1f }, new Reaction { At = -1f }
        };

        // =================================================================== geometry

        private bool IsQuarantined(int x, int y)
        {
            return cellKeys.Contains(Key(new GridPos(minX + x, minY + y)));
        }

        private void EnsureTexture(System.Func<GridPos, Vector2> toWorld)
        {
            int pad = Mathf.CeilToInt(Padding * PixelsPerCell);
            int w = width * PixelsPerCell + pad * 2;
            int h = height * PixelsPerCell + pad * 2;
            if (tex == null || texW != w || texH != h)
            {
                if (tex != null)
                {
                    Destroy(tex);
                }
                texW = w;
                texH = h;
                tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
                tex.hideFlags = HideFlags.HideAndDontSave;
                tex.filterMode = FilterMode.Bilinear;
                tex.wrapMode = TextureWrapMode.Clamp;
                pixels = new Color32[w * h];
                // A fresh Texture2D holds uninitialised memory. If anything ever stops Paint from
                // running, that memory is what covers the board - so it starts transparent.
                tex.SetPixels32(pixels);
                tex.Apply(false, false);
                sdf = new float[w * h];
                prevSdf = new float[w * h];
                revealAt = new float[w * h];
                cellStrength = new float[w * h];
                if (field != null)
                {
                    Destroy(field.gameObject);
                    field = null;
                }
            }
            if (field == null)
            {
                var go = new GameObject("Field");
                go.transform.SetParent(transform, false);
                field = go.AddComponent<SpriteRenderer>();
                field.sortingOrder = FieldOrder;
                spritePpu = 0f;
            }
            float ppu = PixelsPerCell / cellSize;
            if (!Mathf.Approximately(ppu, spritePpu))
            {
                spritePpu = ppu;
                field.sprite = Sprite.Create(tex, new Rect(0, 0, texW, texH),
                    new Vector2(0.5f, 0.5f), ppu);
            }
            // The texture's own bottom-left corner in world space, so a pixel can be turned into
            // a board position and back.
            Vector2 lowCentre = toWorld(new GridPos(minX, minY));
            origin = lowCentre - new Vector2(cellSize * (0.5f + Padding),
                cellSize * (0.5f + Padding));
            Vector2 centre = origin + new Vector2(texW, texH) * (cellSize / PixelsPerCell) * 0.5f;
            field.transform.localPosition = new Vector3(centre.x, centre.y, 0f);
            field.enabled = true;
        }

        /// <summary>
        /// A two pass chamfer distance transform over the occupancy mask, inside and out, giving a
        /// signed distance in cells. Everything the field draws reads off this one array - which
        /// is exactly why interior borders cannot be drawn: they are not in it.
        /// </summary>
        private void BuildDistance(bool fresh)
        {
            System.Array.Copy(sdf, prevSdf, sdf.Length);
            int pad = Mathf.CeilToInt(Padding * PixelsPerCell);
            var inside = new bool[texW * texH];
            for (int py = 0; py < texH; py++)
            {
                int cy = Mathf.FloorToInt((py - pad) / (float)PixelsPerCell);
                for (int px = 0; px < texW; px++)
                {
                    int cx = Mathf.FloorToInt((px - pad) / (float)PixelsPerCell);
                    inside[py * texW + px] = cx >= 0 && cy >= 0 && cx < width && cy < height
                        && IsQuarantined(cx, cy);
                }
            }

            RoundMask(inside);

            var din = new float[texW * texH];
            var dout = new float[texW * texH];
            Chamfer(inside, din, true);
            Chamfer(inside, dout, false);
            float scale = 1f / PixelsPerCell;
            for (int i = 0; i < sdf.Length; i++)
            {
                sdf[i] = (inside[i] ? din[i] : -dout[i]) * scale;
            }
            if (fresh)
            {
                System.Array.Copy(sdf, prevSdf, sdf.Length);
                mergeAt = -1f;
            }
            else
            {
                mergeAt = clock;
            }
            BuildSurface(inside, fresh);
        }

        /// <summary>
        /// Bakes everything about the surface that does not move: dirt, dead patches, scratches
        /// and the soot along the seal. Only pixels that were NOT already sealed are rewritten, so
        /// each new line brings its own wear and the zone grows unevenly, the way a stain does.
        /// </summary>
        private void BuildSurface(bool[] inside, bool fresh)
        {
            // The bake reads the noise table, and the ONLY other thing that builds it is Paint -
            // which runs after this. Without this line the very first seal throws before a single
            // pixel is written, and the field is left showing an unpainted texture.
            BuildLuts();
            if (surface.Length != sdf.Length)
            {
                surface = new float[sdf.Length];
                fresh = true;
            }
            surfaceSeed++;
            int pad = Mathf.CeilToInt(Padding * PixelsPerCell);
            float inv = 1f / PixelsPerCell;
            float seed = surfaceSeed * 13.77f;

            for (int py = 0; py < texH; py++)
            {
                float wy = (py - pad) * inv;
                for (int px = 0; px < texW; px++)
                {
                    int i = py * texW + px;
                    if (!fresh && prevSdf[i] > 0f)
                    {
                        continue;   // already sealed: it keeps the wear it has
                    }
                    if (!inside[i] && sdf[i] < -0.4f)
                    {
                        surface[i] = 0f;
                        continue;
                    }
                    float wx = (px - pad) * inv;

                    // Dirt, shaped so only the top of the range makes a speck at all.
                    float g = NoiseAt(wx * Style.GrainScale + seed,
                        wy * Style.GrainScale + seed * 0.7f);
                    g = g > Style.GrainSparsity
                        ? (g - Style.GrainSparsity) / (1f - Style.GrainSparsity) : 0f;

                    // Patches where more of the life was taken. One-sided: never lighter.
                    float p = NoiseAt(wx * Style.PatchScale + seed * 0.3f,
                        wy * Style.PatchScale - seed * 0.5f);
                    p = Mathf.Max(0f, p) * Style.PatchStrength;

                    // The middle octave: tighter than the broad patches, looser than the grain.
                    float st = NoiseAt(wx * Style.StainScale - seed * 0.9f,
                        wy * Style.StainScale + seed * 1.4f);
                    st = Mathf.Max(0f, st) * Style.StainStrength;

                    // Actual patches, thresholded so most of the surface has none.
                    float bl = NoiseAt(wx * Style.BlotchScale + seed * 2.1f,
                        wy * Style.BlotchScale - seed * 1.7f) * 0.5f + 0.5f;
                    float keepB = 1f - Style.BlotchCoverage;
                    bl = bl > keepB ? (bl - keepB) / Mathf.Max(Style.BlotchCoverage, 0.01f) : 0f;
                    bl = bl * bl * Style.BlotchStrength;   // squared: soft edged, and rarer

                    surface[i] = g * Style.GrainStrength + p + st + bl;
                }
            }

            BakeSoot(fresh);
            BakeScratches(inside, fresh);
            CollectSeam();
        }

        /// <summary>
        /// Soot along the seal, heaviest where the perimeter turns. Cornerness is measured as how
        /// far the neighbourhood departs from half-inside: a straight run is exactly half, and both
        /// a convex and a concave corner are not - which is right, because grime gathers at every
        /// turn, not only the ones pointing one way.
        /// </summary>
        private void BakeSoot(bool fresh)
        {
            const int r = 4;
            for (int py = r; py < texH - r; py++)
            {
                for (int px = r; px < texW - r; px++)
                {
                    int i = py * texW + px;
                    if (!fresh && prevSdf[i] > 0f)
                    {
                        continue;
                    }
                    float d = sdf[i];
                    if (d < 0f || d > 0.30f)
                    {
                        continue;   // soot lives on the inside lip and nowhere else
                    }
                    int inCount = 0;
                    int total = 0;
                    for (int y = -r; y <= r; y++)
                    {
                        for (int x = -r; x <= r; x++)
                        {
                            if (x * x + y * y > r * r)
                            {
                                continue;
                            }
                            total++;
                            if (sdf[(py + y) * texW + px + x] > 0f)
                            {
                                inCount++;
                            }
                        }
                    }
                    float frac = total > 0 ? inCount / (float)total : 0.5f;
                    float corner = Mathf.Clamp01(Mathf.Abs(frac - 0.5f) * 2.4f);
                    float near = 1f - d / 0.30f;
                    surface[i] += Style.SootEdgeStrength * near
                        * (1f + corner * Style.SootCornerBoost);
                }
            }
        }

        /// <summary>A few dry scratches, stamped straight into the bake. Rasterised along their
        /// own line rather than tested per pixel, which is why they cost nothing.</summary>
        private void BakeScratches(bool[] inside, bool fresh)
        {
            int cells = 0;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (IsQuarantined(x, y))
                    {
                        cells++;
                    }
                }
            }
            int want = Mathf.Clamp(
                Mathf.RoundToInt(cells * Style.ScratchDensity / 10f), 0, 40);
            int pad = Mathf.CeilToInt(Padding * PixelsPerCell);
            for (int n = 0; n < want; n++)
            {
                int sx = 0;
                int sy = 0;
                bool found = false;
                for (int tries = 0; tries < 30; tries++)
                {
                    sx = Random.Range(pad, texW - pad);
                    sy = Random.Range(pad, texH - pad);
                    int i = sy * texW + sx;
                    if (!inside[i])
                    {
                        continue;
                    }
                    if (!fresh && prevSdf[i] > 0f)
                    {
                        continue;   // do not scratch a surface that already has its own
                    }
                    found = true;
                    break;
                }
                if (!found)
                {
                    continue;
                }
                float ang = Random.Range(0f, Mathf.PI * 2f);
                float len = Random.Range(Style.ScratchLengthMin, Style.ScratchLengthMax)
                    * PixelsPerCell;
                float dx = Mathf.Cos(ang);
                float dy = Mathf.Sin(ang);
                int steps = Mathf.RoundToInt(len);
                for (int s = 0; s < steps; s++)
                {
                    // Fading at both ends, so a scratch has no start and no stop.
                    float along = s / (float)Mathf.Max(steps - 1, 1);
                    float taper = Mathf.Sin(along * Mathf.PI);
                    int x = sx + Mathf.RoundToInt(dx * s);
                    int y = sy + Mathf.RoundToInt(dy * s);
                    if (x < 1 || y < 1 || x >= texW - 1 || y >= texH - 1)
                    {
                        break;
                    }
                    int i = y * texW + x;
                    if (sdf[i] <= 0.05f)
                    {
                        break;   // never runs off the edge of the film
                    }
                    surface[i] -= Style.ScratchOpacity * taper;
                    surface[i - 1] -= Style.ScratchOpacity * taper * 0.35f;
                    surface[i + 1] -= Style.ScratchOpacity * taper * 0.35f;
                }
            }
        }

        /// <summary>Anchor points on the perimeter, thinned out, for the seam flicker.</summary>
        private void CollectSeam()
        {
            seamPixels.Clear();
            for (int i = 0; i < sdf.Length; i += 7)
            {
                float d = sdf[i];
                if (d > 0.02f && d < 0.12f)
                {
                    seamPixels.Add(i);
                }
            }
        }

        /// <summary>
        /// Rounds the cell mask before any distance is measured from it. A mask built from whole
        /// cells has nothing but right angles, and every contour taken from it inherits them - the
        /// outside corners AND the inside corners of an L or a cross. Blurring the mask and taking
        /// it back to a hard edge at the halfway point rounds both at once, by the blur radius,
        /// which is the cheapest honest way to get a perimeter that was not cut out of a grid.
        /// </summary>
        private void RoundMask(bool[] inside)
        {
            // Two passes, thresholded off-centre, which is an opening followed by a closing:
            // the first rounds the OUTSIDE corners, the second the inside ones of an L or a cross
            // and puts the area back. Doing it in one symmetric pass gives both the same radius,
            // and they do not want the same radius.
            Soften(inside, Style.CornerRadiusOuter, 0.60f);
            Soften(inside, Style.CornerRadiusInner, 0.40f);
        }

        private void Soften(bool[] inside, float radiusCells, float threshold)
        {
            int r = Mathf.Max(1, Mathf.RoundToInt(radiusCells * PixelsPerCell));
            var a = new float[inside.Length];
            var b = new float[inside.Length];
            for (int i = 0; i < inside.Length; i++)
            {
                a[i] = inside[i] ? 1f : 0f;
            }
            BlurX(a, b, r);
            BlurY(b, a, r);
            for (int i = 0; i < inside.Length; i++)
            {
                inside[i] = a[i] >= threshold;
            }
        }

        private void BlurX(float[] src, float[] dst, int r)
        {
            for (int y = 0; y < texH; y++)
            {
                int row = y * texW;
                for (int x = 0; x < texW; x++)
                {
                    float sum = 0f;
                    int n = 0;
                    for (int k = -r; k <= r; k++)
                    {
                        int xx = x + k;
                        if (xx < 0 || xx >= texW)
                        {
                            continue;
                        }
                        sum += src[row + xx];
                        n++;
                    }
                    dst[row + x] = n > 0 ? sum / n : 0f;
                }
            }
        }

        private void BlurY(float[] src, float[] dst, int r)
        {
            for (int x = 0; x < texW; x++)
            {
                for (int y = 0; y < texH; y++)
                {
                    float sum = 0f;
                    int n = 0;
                    for (int k = -r; k <= r; k++)
                    {
                        int yy = y + k;
                        if (yy < 0 || yy >= texH)
                        {
                            continue;
                        }
                        sum += src[yy * texW + x];
                        n++;
                    }
                    dst[y * texW + x] = n > 0 ? sum / n : 0f;
                }
            }
        }

        /// <summary>Distance to the nearest pixel NOT of the given class, in pixels.</summary>
        private void Chamfer(bool[] inside, float[] d, bool forInside)
        {
            const float o = 1f;
            const float dg = 1.41421356f;
            float big = texW + texH;
            for (int i = 0; i < d.Length; i++)
            {
                d[i] = inside[i] == forInside ? big : 0f;
            }
            for (int y = 0; y < texH; y++)
            {
                for (int x = 0; x < texW; x++)
                {
                    int i = y * texW + x;
                    float v = d[i];
                    if (x > 0) { v = Mathf.Min(v, d[i - 1] + o); }
                    if (y > 0) { v = Mathf.Min(v, d[i - texW] + o); }
                    if (x > 0 && y > 0) { v = Mathf.Min(v, d[i - texW - 1] + dg); }
                    if (x < texW - 1 && y > 0) { v = Mathf.Min(v, d[i - texW + 1] + dg); }
                    d[i] = v;
                }
            }
            for (int y = texH - 1; y >= 0; y--)
            {
                for (int x = texW - 1; x >= 0; x--)
                {
                    int i = y * texW + x;
                    float v = d[i];
                    if (x < texW - 1) { v = Mathf.Min(v, d[i + 1] + o); }
                    if (y < texH - 1) { v = Mathf.Min(v, d[i + texW] + o); }
                    if (x < texW - 1 && y < texH - 1) { v = Mathf.Min(v, d[i + texW + 1] + dg); }
                    if (x > 0 && y < texH - 1) { v = Mathf.Min(v, d[i + texW - 1] + dg); }
                    d[i] = v;
                }
            }
        }

        /// <summary>Per pixel field strength from cell occupancy, sampled bilinearly so a filled
        /// cell weakens the membrane as a gradient rather than as a square.</summary>
        private void BuildStrength(System.Func<GridPos, bool> occupied)
        {
            var perCell = new float[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    bool full = occupied != null && occupied(new GridPos(minX + x, minY + y));
                    perCell[y * width + x] = full
                        ? Style.FilledCellOverlayStrength : Style.EmptyCellOverlayStrength;
                }
            }
            int pad = Mathf.CeilToInt(Padding * PixelsPerCell);
            for (int py = 0; py < texH; py++)
            {
                float fy = (py - pad) / (float)PixelsPerCell - 0.5f;
                for (int px = 0; px < texW; px++)
                {
                    float fx = (px - pad) / (float)PixelsPerCell - 0.5f;
                    cellStrength[py * texW + px] = Bilinear(perCell, fx, fy);
                }
            }
        }

        private float Bilinear(float[] grid, float fx, float fy)
        {
            int x0 = Mathf.Clamp(Mathf.FloorToInt(fx), 0, width - 1);
            int y0 = Mathf.Clamp(Mathf.FloorToInt(fy), 0, height - 1);
            int x1 = Mathf.Clamp(x0 + 1, 0, width - 1);
            int y1 = Mathf.Clamp(y0 + 1, 0, height - 1);
            float tx = Mathf.Clamp01(fx - x0);
            float ty = Mathf.Clamp01(fy - y0);
            float a = Mathf.Lerp(grid[y0 * width + x0], grid[y0 * width + x1], tx);
            float b = Mathf.Lerp(grid[y1 * width + x0], grid[y1 * width + x1], tx);
            return Mathf.Lerp(a, b, ty);
        }

        /// <summary>
        /// Gives every pixel of a NEWLY sealed line the moment its own seal reaches it, measured
        /// from the board edge that line was taken from. The boss always seals the outermost clean
        /// line, so which half of the board a line sits in is enough to say which side it came
        /// from - no extra information has to cross from Core.
        /// </summary>
        /// <summary>
        /// When each pixel's seal arrives. A new cell blooms from its own centre outward, one
        /// after another, so a patch of three cells reads as three seals closing rather than one
        /// rectangle appearing. Pixels that were already sealed are left alone - that is what
        /// keeps a cell landing beside an existing one from re-sweeping its neighbour.
        /// </summary>
        private void BuildReveal(List<GridPos> added, bool fresh)
        {
            for (int i = 0; i < revealAt.Length; i++)
            {
                revealAt[i] = -1f;
            }
            if (fresh || added.Count == 0)
            {
                sealEndsAt = -1f;
                lockAt = -1f;
                return;
            }

            int pad = Mathf.CeilToInt(Padding * PixelsPerCell);
            float latest = 0f;
            // The bloom reaches a little past the cell so the film joins up with whatever it
            // lands against, and the pixels between two new cells are covered by both.
            float reach = PixelsPerCell * 0.5f + RevealMargin;
            for (int c = 0; c < added.Count; c++)
            {
                int cx = added[c].X - minX;
                int cy = added[c].Y - minY;
                if (cx < 0 || cy < 0 || cx >= width || cy >= height)
                {
                    continue;
                }
                // Staggered, so several cells sealed at once arrive as a run of events rather
                // than one flat flash.
                float start = clock + Style.WarningDuration + c * Style.SweepStagger;
                float centreX = pad + (cx + 0.5f) * PixelsPerCell;
                float centreY = pad + (cy + 0.5f) * PixelsPerCell;
                int lox = Mathf.Max(0, Mathf.FloorToInt(centreX - reach));
                int hix = Mathf.Min(texW, Mathf.CeilToInt(centreX + reach));
                int loy = Mathf.Max(0, Mathf.FloorToInt(centreY - reach));
                int hiy = Mathf.Min(texH, Mathf.CeilToInt(centreY + reach));
                for (int py = loy; py < hiy; py++)
                {
                    for (int px = lox; px < hix; px++)
                    {
                        int i = py * texW + px;
                        // Never re-seal what was already sealed.
                        if (prevSdf[i] > 0f)
                        {
                            continue;
                        }
                        float dx = px - centreX;
                        float dy = py - centreY;
                        float dist = Mathf.Sqrt(dx * dx + dy * dy);
                        if (dist > reach)
                        {
                            continue;
                        }
                        float t = start + Style.SweepDuration * (dist / reach);
                        // Where two new cells overlap, the EARLIER seal wins - the pixel belongs
                        // to one front, not to two.
                        if (revealAt[i] < 0f || t < revealAt[i])
                        {
                            revealAt[i] = t;
                        }
                        latest = Mathf.Max(latest, t);
                    }
                }
            }
            sealEndsAt = latest + Style.SweepSettle;
            lockAt = sealEndsAt;
        }

        // =================================================================== painting

        private static float[] bodyLut;

        private static float[] edgeLut;

        private static float[] innerLut;

        private static float[] hiLut;

        private static float[] shLut;

        private static float[] noiseTable;

        private const int NoiseSize = 64;

        /// <summary>The whole cross-section baked once into lookups. Every pixel of the field asks
        /// these the same four questions about its distance from the seal, and doing that with
        /// exponentials per pixel per frame is what would make a board-sized field expensive.</summary>
        private static void BuildLuts()
        {
            if (bodyLut != null)
            {
                return;
            }
            bodyLut = new float[LutSize];
            edgeLut = new float[LutSize];
            innerLut = new float[LutSize];
            hiLut = new float[LutSize];
            shLut = new float[LutSize];
            for (int i = 0; i < LutSize; i++)
            {
                float d = Mathf.Lerp(-LutRange, LutRange, i / (float)(LutSize - 1));
                bodyLut[i] = Mathf.Clamp01(0.5f + 0.5f * d / Mathf.Max(Style.EdgeSoftness, 0.001f));
                // Flat across the seal, then a short ramp. Thin by design.
                float e = Mathf.Max(0f, d - Style.OuterSealWidth)
                    / Mathf.Max(Style.SealFalloff, 0.001f);
                edgeLut[i] = d < 0f ? 0f : Mathf.Exp(-e * e);
                // The wide, weak absorption behind it - a separate thing from the seal, and the
                // only one of the two allowed to reach any distance into the region.
                float ins = d < 0f ? 3f : d / Mathf.Max(Style.InnerShadowWidth, 0.001f);
                innerLut[i] = Mathf.Exp(-ins * ins);
                float wn = (d - Style.AmberResidueOffset)
                    / Mathf.Max(Style.OuterSealWidth * 1.6f, 0.001f);
                hiLut[i] = Mathf.Exp(-wn * wn);
                float s = d >= 0f ? 3f : d / Mathf.Max(Style.EdgeShadowWidth, 0.001f);
                shLut[i] = Mathf.Exp(-s * s);
            }
            noiseTable = new float[NoiseSize * NoiseSize];
            var rng = new System.Random(20601);
            for (int i = 0; i < noiseTable.Length; i++)
            {
                noiseTable[i] = (float)rng.NextDouble();
            }
        }

        private static int LutIndex(float d)
        {
            float k = (d + LutRange) / (2f * LutRange);
            return Mathf.Clamp((int)(k * (LutSize - 1)), 0, LutSize - 1);
        }

        private static float NoiseAt(float x, float y)
        {
            int xi = Mathf.FloorToInt(x);
            int yi = Mathf.FloorToInt(y);
            float fx = x - xi;
            float fy = y - yi;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            int x0 = ((xi % NoiseSize) + NoiseSize) % NoiseSize;
            int y0 = ((yi % NoiseSize) + NoiseSize) % NoiseSize;
            int x1 = (x0 + 1) % NoiseSize;
            int y1 = (y0 + 1) % NoiseSize;
            float a = Mathf.Lerp(noiseTable[y0 * NoiseSize + x0], noiseTable[y0 * NoiseSize + x1], fx);
            float b = Mathf.Lerp(noiseTable[y1 * NoiseSize + x0], noiseTable[y1 * NoiseSize + x1], fx);
            return Mathf.Lerp(a, b, fy) * 2f - 1f;
        }

        private void Paint()
        {
            BuildLuts();
            int pad = Mathf.CeilToInt(Padding * PixelsPerCell);
            float inv = 1f / PixelsPerCell;
            float hazeX = clock * Style.MottlingSpeed;
            float hazeY = clock * Style.MottlingSpeed * -0.62f;

            float merge = mergeAt < 0f ? 0f
                : 1f - Mathf.Clamp01((clock - mergeAt) / Style.MergeDissolve);
            float lockPulse = lockAt < 0f ? 0f
                : Mathf.Clamp01(1f - (clock - lockAt) / Style.LockPulseDuration);
            lockPulse *= lockPulse;

            // The film seating itself once the front has gone by: pressed a little harder for a
            // moment, then easing back. Small on purpose - a vacuum, not a scale animation.
            float settlePress = 1f;
            if (sealEndsAt >= 0f)
            {
                float since = clock - sealEndsAt;
                if (since >= 0f && since < Style.FilmSettleDuration)
                {
                    float k = 1f - since / Style.FilmSettleDuration;
                    settlePress = 1f + k * k * Style.FilmSettlePress;
                }
            }

            float flickerLevel = 0f;
            if (scanStartedAt >= 0f)
            {
                float k = (clock - scanStartedAt) / Style.FlickerDuration;
                if (k >= 0f && k <= 1f)
                {
                    // Struggling rather than pulsing: two beats inside one short flicker.
                    flickerLevel = Mathf.Sin(k * Mathf.PI) * Style.WarningSeamFlickerStrength
                        * (0.55f + 0.45f * Mathf.Sin(k * Mathf.PI * 5f));
                }
            }

            Color film = Style.FilmColor;
            Color sealCol = Style.SealBandColor;
            Color warnCol = Style.WarningColor;
            Color scanCol = Style.ScanColor;
            Color reactCol = Style.ReactionColor;

            for (int py = 0; py < texH; py++)
            {
                float wy = (py - pad) * inv;
                int row = py * texW;
                for (int px = 0; px < texW; px++)
                {
                    int i = row + px;
                    float d = sdf[i];
                    float wx = (px - pad) * inv;

                    // Well outside the mask there is nothing to draw at all. The margin used to
                    // carry the edge warning of an incoming seal; the zone no longer comes in from
                    // an edge, so the margin is simply empty.
                    if (d < -LutRange)
                    {
                        pixels[i] = new Color32(0, 0, 0, 0);
                        continue;
                    }

                    // How far this pixel's own seal has got. Everything else is multiplied by it,
                    // so nothing exists ahead of the front.
                    float rev = revealAt[i];
                    // How far this pixel has SETTLED into membrane behind the front. The front
                    // itself is deliberately not multiplied by it: at the front this is zero, so
                    // gating the wave by it would erase exactly the bright edge that is the whole
                    // point of a seal sweeping in rather than a rectangle fading up.
                    float settled = 1f;
                    float front = 0f;
                    if (rev >= 0f)
                    {
                        float since = clock - rev;
                        if (since < -Style.SweepWaveWidth * 3f)
                        {
                            pixels[i] = new Color32(0, 0, 0, 0);
                            continue;
                        }
                        settled = Mathf.Clamp01(since / Style.SweepSettle);
                        float w = since / Style.SweepWaveWidth;
                        // The front adds MORE of the dark film, so the moment the seal passes is
                        // the darkest the area ever gets and it eases back up to the settled
                        // level behind. Nothing about this wave is a light.
                        front = Mathf.Exp(-w * w);
                    }

                    int li = LutIndex(d);
                    float inField = bodyLut[li];
                    // The seal's weight wanders along the perimeter: clear in places, nearly
                    // buried under grime in others.
                    float sealNoise = 1f - Style.SealBreakUp * 0.5f * (1f
                        - NoiseAt(wx * Style.SealBreakUpScale - 61f,
                            wy * Style.SealBreakUpScale + 23f));
                    float seal = edgeLut[li] * Style.OuterSealDarkness * sealNoise
                        + innerLut[li] * Style.InnerShadowStrength;
                    // The residue survives in patches. Sampled in BOARD space, so which stretches
                    // still carry it has nothing to do with where the cells are.
                    float res = NoiseAt(wx * Style.AmberResidueScale + 91f,
                        wy * Style.AmberResidueScale + 47f) * 0.5f + 0.5f;
                    float keep = 1f - Style.AmberResidueCoverage;
                    res = res > keep ? (res - keep) / Mathf.Max(Style.AmberResidueCoverage, 0.01f)
                        : 0f;
                    // A second, much finer break-up on top, so the stretches that DID survive are
                    // themselves worn through in places rather than being clean little arcs.
                    res *= Mathf.Clamp01(0.40f + 0.60f
                        * (NoiseAt(wx * Style.AmberResidueScale * 4.1f + 12f,
                            wy * Style.AmberResidueScale * 4.1f - 5f) * 0.5f + 0.5f));
                    float warnEdge = hiLut[li] * Style.AmberResidueOpacity * res
                        * (1f + lockPulse * Style.LockPulseStrength);
                    float sh = shLut[li] * Style.EdgeShadow;

                    // A border that has just become interior is drawn from the OLD field and faded
                    // out. Without this the shared edge simply stops existing on one frame, which
                    // is what makes two regions look like they were swapped rather than merged.
                    if (merge > 0f && d > Style.AmberResidueOffset)
                    {
                        int pi = LutIndex(prevSdf[i]);
                        seal += edgeLut[pi] * Style.OuterSealDarkness * merge;
                    }

                    float strength = cellStrength[i];
                    // Two octaves of smoke, the only part of the surface that moves. Everything
                    // else was baked when the shape last changed.
                    float mn = NoiseAt(wx * Style.MottlingScale + hazeX,
                            wy * Style.MottlingScale + hazeY) * 0.66f
                        + NoiseAt(wx * Style.MottlingScale * 2.3f - hazeY,
                            wy * Style.MottlingScale * 2.3f + hazeX) * 0.34f;
                    // Pushed toward its own extremes before it is scaled, so the surface has
                    // distinct smoky areas instead of an even wobble about the middle.
                    mn = Mathf.Sign(mn) * Mathf.Pow(Mathf.Abs(mn), Style.MottlingContrast);
                    float mottle = mn * Style.MottlingStrength;
                    float wear = surface[i];

                    // A short stretch of seam catching for a moment - and only where the seam
                    // actually is, so it can never light the middle of the region.
                    float scanTerm = 0f;
                    if (flickerLevel > 0f)
                    {
                        float fdx = wx - flickerX;
                        float fdy = wy - flickerY;
                        float rr = (fdx * fdx + fdy * fdy)
                            / (Style.FlickerRadius * Style.FlickerRadius);
                        scanTerm = Mathf.Exp(-rr) * flickerLevel * edgeLut[LutIndex(d)];
                    }

                    float react = 0f;
                    for (int r = 0; r < reactions.Length; r++)
                    {
                        float at = reactions[r].At;
                        if (at < 0f)
                        {
                            continue;
                        }
                        float age = (clock - at) / Style.ReactionDuration;
                        if (age < 0f || age > 1f)
                        {
                            continue;
                        }
                        Vector2 w2 = reactions[r].Where - origin;
                        float dx = wx - w2.x / cellSize;
                        float dy = wy - w2.y / cellSize;
                        float rr = (dx * dx + dy * dy) / (Style.ReactionRadius * Style.ReactionRadius);
                        react += Mathf.Exp(-rr) * (1f - age) * Style.ReactionStrength;
                    }

                    // THE TEXTURE IS A MULTIPLIER ON THE FILM'S COLOUR. Every surface layer lands
                    // here, on the one channel that has room to show them.
                    // The surface does not arrive all at once behind the front: the smoke is
                    // there almost immediately, the dirt and staining take a moment longer, and
                    // the seal is last. Powers of the same settle, so nothing needs its own clock.
                    float texEarly = settled < 1f ? Mathf.Pow(settled, 0.55f) : 1f;
                    float texLate = settled < 1f ? Mathf.Pow(settled, 1.6f) : 1f;
                    float tex = 1f + mottle * texEarly - wear * texLate;
                    // Last of all, and only once the surface under it has settled.
                    tex *= 1f - seal * (settled < 1f ? settled * settled : 1f);
                    tex *= 1f - front * Style.SweepDarkenStrength;  // the wave kills as it passes
                    tex = Mathf.Clamp(tex, 0.22f, 1.85f);

                    float glint = front * Style.SweepGlint;
                    float filmA = Style.DeadZoneOpacity * inField * strength * settlePress;
                    float a = (filmA + warnEdge * inField * 0.45f + scanTerm * inField) * settled
                        + react * inField * 0.55f;
                    a = Mathf.Clamp01(a);
                    if (a <= 0.002f && sh <= 0.002f)
                    {
                        pixels[i] = new Color32(0, 0, 0, 0);
                        continue;
                    }

                    // Colour: the film, pushed DARKER at the rim and only barely toward amber for
                    // the warning line, the glint on the front and the rare scan. Everything here
                    // is a subtraction of light except those three accents.
                    float warnW = Mathf.Clamp01(warnEdge * inField + glint * inField
                        + scanTerm * inField);
                    float rw = Mathf.Clamp01(react);
                    float cr = film.r * tex;
                    float cg = film.g * tex;
                    float cb = film.b * tex;
                    cr = Mathf.Lerp(cr, warnCol.r, warnW);
                    cg = Mathf.Lerp(cg, warnCol.g, warnW);
                    cb = Mathf.Lerp(cb, warnCol.b, warnW);
                    cr = Mathf.Lerp(cr, reactCol.r, rw);
                    cg = Mathf.Lerp(cg, reactCol.g, rw);
                    cb = Mathf.Lerp(cb, reactCol.b, rw);

                    // The one thing that lives OUTSIDE the field: it darkens the board just past
                    // the rim, so the dead area bleeds into the live board rather than ending.
                    if (sh > 0.002f)
                    {
                        float sa = sh * (1f - inField) * settled;
                        float total = a + sa;
                        if (total > 0.0001f)
                        {
                            cr = (cr * a + sealCol.r * sa) / total;
                            cg = (cg * a + sealCol.g * sa) / total;
                            cb = (cb * a + sealCol.b * sa) / total;
                        }
                        a = Mathf.Clamp01(total);
                    }

                    pixels[i] = new Color32(
                        (byte)Mathf.RoundToInt(Mathf.Clamp01(cr) * 255f),
                        (byte)Mathf.RoundToInt(Mathf.Clamp01(cg) * 255f),
                        (byte)Mathf.RoundToInt(Mathf.Clamp01(cb) * 255f),
                        (byte)Mathf.RoundToInt(a * 255f));
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, false);
            repaintedAt = clock;
        }

        // =================================================================== motes

        private struct Mote
        {
            public SpriteRenderer R;
            public Vector2 Pos;
            public Vector2 Vel;
            public float Size;
            public float Seed;
            public bool Live;
        }

        private Mote[] motes = new Mote[0];

        private static Sprite moteSprite;

        private static Sprite MoteSprite()
        {
            if (moteSprite != null)
            {
                return moteSprite;
            }
            const int n = 16;
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float u = ((x + 0.5f) / n - 0.5f) * 2f;
                    float v = ((y + 0.5f) / n - 0.5f) * 2f;
                    float a = Mathf.Max(0f, Mathf.Exp(-3.4f * (u * u + v * v)) - 0.03f);
                    px[y * n + x] = new Color32(255, 255, 255,
                        (byte)Mathf.RoundToInt(Mathf.Clamp01(a) * 255f));
                }
            }
            moteSprite = MakeSprite(px, n);
            return moteSprite;
        }

        private static Sprite MakeSprite(Color32[] px, int n)
        {
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false);
            t.hideFlags = HideFlags.HideAndDontSave;
            t.filterMode = FilterMode.Bilinear;
            t.wrapMode = TextureWrapMode.Clamp;
            t.SetPixels32(px);
            t.Apply(false, false);
            return Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
        }

        private void RebuildMotes()
        {
            int cells = 0;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (IsQuarantined(x, y))
                    {
                        cells++;
                    }
                }
            }
            int want = Mathf.Clamp(Mathf.RoundToInt(cells / Mathf.Max(Style.MoteCellsEach, 1f)),
                0, 16);
            while (motes.Length < want)
            {
                var next = new Mote[motes.Length + 1];
                System.Array.Copy(motes, next, motes.Length);
                var go = new GameObject("Mote" + motes.Length);
                go.transform.SetParent(transform, false);
                var r = go.AddComponent<SpriteRenderer>();
                r.sprite = MoteSprite();
                r.sortingOrder = MoteOrder;
                r.enabled = false;
                next[motes.Length].R = r;
                motes = next;
            }
            for (int i = 0; i < motes.Length; i++)
            {
                if (i < want)
                {
                    if (!motes[i].Live)
                    {
                        Respawn(i);
                    }
                }
                else
                {
                    motes[i].Live = false;
                    motes[i].R.enabled = false;
                }
            }
        }

        private void Respawn(int i)
        {
            for (int tries = 0; tries < 24; tries++)
            {
                int x = Random.Range(0, width);
                int y = Random.Range(0, height);
                if (!IsQuarantined(x, y))
                {
                    continue;
                }
                motes[i].Pos = new Vector2(x + Random.value, y + Random.value);
                float a = Random.Range(0f, Mathf.PI * 2f);
                motes[i].Vel = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Style.MoteSpeed
                    * Random.Range(0.5f, 1f);
                motes[i].Size = Random.Range(Style.MoteSizeMin, Style.MoteSizeMax);
                motes[i].Seed = Random.value * 10f;
                motes[i].Live = true;
                motes[i].R.enabled = true;
                return;
            }
            motes[i].Live = false;
            motes[i].R.enabled = false;
        }

        private void StepMotes(float dt)
        {
            for (int i = 0; i < motes.Length; i++)
            {
                if (!motes[i].Live)
                {
                    continue;
                }
                motes[i].Pos += motes[i].Vel * dt;
                int cx = Mathf.FloorToInt(motes[i].Pos.x);
                int cy = Mathf.FloorToInt(motes[i].Pos.y);
                if (cx < 0 || cy < 0 || cx >= width || cy >= height || !IsQuarantined(cx, cy))
                {
                    Respawn(i);
                    continue;
                }
                Vector2 world = origin + new Vector2(
                    (motes[i].Pos.x + Padding) * cellSize, (motes[i].Pos.y + Padding) * cellSize);
                Transform t = motes[i].R.transform;
                t.localPosition = new Vector3(world.x, world.y, 0f);
                float s = motes[i].Size * cellSize;
                t.localScale = new Vector3(s, s, 1f);
                float breathe = 0.75f + 0.25f * Mathf.Sin(clock * 0.9f + motes[i].Seed);
                Color c = Style.MoteColor;
                c.a = Style.MoteOpacity * breathe;
                motes[i].R.color = c;
            }
        }

        // =================================================================== running

        private void Update()
        {
            if (!built)
            {
                return;
            }
            float dt = Time.deltaTime;
            clock += dt;
            StepMotes(dt);

            if (scanStartedAt < 0f && clock >= nextScanAt)
            {
                StartScan();
            }
            else if (scanStartedAt >= 0f && clock - scanStartedAt > Style.FlickerDuration)
            {
                scanStartedAt = -1f;
                nextScanAt = clock
                    + Random.Range(Style.FlickerEveryMin, Style.FlickerEveryMax);
            }

            // Every frame while something is moving fast; a third of that when the field is just
            // sitting there. The haze and the scan are slow enough not to notice, and this field
            // can cover most of a board.
            bool busy = (sealEndsAt >= 0f && clock <= sealEndsAt + Style.FilmSettleDuration)
                || (mergeAt >= 0f && clock - mergeAt < Style.MergeDissolve)
                || scanStartedAt >= 0f
                || AnyReaction();
            if (busy || clock - repaintedAt >= 1f / IdleRepaintHz)
            {
                Paint();
            }
        }

        private bool AnyReaction()
        {
            for (int i = 0; i < reactions.Length; i++)
            {
                if (reactions[i].At >= 0f && clock - reactions[i].At <= Style.ReactionDuration)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Picks one point on the perimeter and lets that stretch catch for a moment.
        /// A different point every time, so nothing about it reads as a system on a cycle.</summary>
        private void StartScan()
        {
            if (seamPixels.Count == 0)
            {
                nextScanAt = clock + Style.FlickerEveryMax;
                return;
            }
            int i = seamPixels[Random.Range(0, seamPixels.Count)];
            int pad = Mathf.CeilToInt(Padding * PixelsPerCell);
            flickerX = (i % texW - pad) / (float)PixelsPerCell;
            flickerY = (i / texW - pad) / (float)PixelsPerCell;
            scanStartedAt = clock;
        }

        private void OnDestroy()
        {
            if (tex != null)
            {
                Destroy(tex);
            }
        }
    }
}
