// PURPOSE: What a cleared LINE looks like BEHIND the drawn explosion - energy leaving the
// middle and running to both ends, rather than the row being painted. LineBurstView is the
// hand-drawn star in the centre; this is everything around it, and the two are deliberately
// separate so the art can be redrawn without touching the beam or the other way round.
//
// IT REPLACED A ROW OF LIT SQUARES. A line used to go through CellFlashFx like every other
// destruction: each cell struck as a flat square at 1.02x its size, so the gaps closed and the
// row read as one continuous bar. That is exactly right for a loose handful of cells and
// exactly wrong for a full line - eleven squares at full opacity ARE a rectangle, and no
// amount of colour makes a rectangle look like an explosion. CellFlashFx still runs every
// other blast; the line is the one destruction that now has its own language.
//
// THE BEAM IS ONE QUAD WITH THREE LAYERS PAINTED INTO IT, not three stacked renderers. Core,
// glow and aura are concentric bands of one vertical profile, so they can never drift apart,
// they cost one draw call, and - the real reason - their SHAPE can vary along the row. Three
// stacked quads would each be a rectangle; a painted profile can narrow toward the ends, waver,
// and carry a bright head that moves, which is what makes this read as travelling energy.
//
// THE TEXTURE IS REPAINTED WHILE THE WAVE MOVES AND THEN FROZEN. Everything interesting happens
// in the first ~0.15s - the front crossing the row, the head brightening what it passes. After
// the peak nothing about the SHAPE changes, only how bright it all is, and a renderer tint does
// that for free. So the repaint stops at the peak and the fade is a colour ramp: half the cost,
// and the silhouette cannot crawl while it dies.
//
// COLOUR IS AN ARGUMENT, NEVER A CONSTANT. The tone comes in from the caller - the streak tier
// for a line, and whatever else asks later - and the ramp is built from it: the aura is that
// colour thinned, the core is that colour driven to white. Nothing here knows what green or
// purple mean, which is why a new tier needs no change in this file.
//
// THE CELLS ANSWER SEPARATELY. The beam is smooth along the row on purpose - it is a beam, not
// a row of lamps - so on its own it would float over the grid without belonging to it. Each
// cleared cell therefore lights its own slot as the front passes: an outline, a faint fill and
// a small swell, gone in a sixth of a second. That is the layer that ties the effect to a game
// made of squares, and it is kept deliberately short so it never competes with the beam.

using System.Collections.Generic;
using UnityEngine;

namespace ProjectBlock.View
{
    /// <summary>The layered energy sweep along a cleared line. Fire and forget: Play, then it
    /// runs itself out.</summary>
    public sealed class LineSweepView : MonoBehaviour
    {
        // =================================================================== TUNING
        /// <summary>Everything that decides how the sweep READS, in one place. Widths are in
        /// CELLS rather than world units, so the beam keeps the same relationship to the grid on
        /// a 7x7 and an 11x11 - unlike the drawn burst, which is a fixed world size.</summary>
        public static class Style
        {
            /// <summary>Half-width of the bright core, in cells. This is the "power line" and it
            /// is deliberately thin: the width of the effect is supposed to come from the glow
            /// around it, not from a thick opaque middle.</summary>
            public static float CoreWidth = 0.30f;

            /// <summary>Half-width of the translucent energy field around the core, in cells.
            /// This is the band that gives the row its sense of width.</summary>
            public static float GlowWidth = 0.62f;

            /// <summary>What the glow and the haze contribute NEXT TO the core, which is always
            /// 1. These decide whether the beam reads as a bright line inside a glow or as one
            /// soft smear: push them up and the core stops being findable, which is the failure
            /// this effect was made to get away from.</summary>
            public static float GlowGain = 0.55f;

            public static float AuraGain = 0.42f;

            /// <summary>Half-width of the outermost haze, in cells. Reaches past a cell on each
            /// side so the line has no hard boundary, but stays well short of swamping its
            /// neighbours - the grid has to stay readable underneath.</summary>
            public static float AuraWidth = 1.30f;

            /// <summary>How long the front takes to travel from the middle to the ends. Short:
            /// the player should feel it sweep, not watch it arrive. These sat at 0.105 and were
            /// halved, then eased back 25%: the whole line clear now runs at 1.6x its original
            /// speed. Every timing in this block moves TOGETHER by that factor, and so does
            /// LineBurstView.Style.Fps - see the note on Attack for why.</summary>
            public static float PropagationSeconds = 0.066f;

            /// <summary>How long the whole line sits at full strength once the front lands.</summary>
            public static float PeakHold = 0.034f;

            /// <summary>The dissolve. Longer than the spread, because energy leaving is what
            /// makes it feel like energy rather than a switch being flipped.</summary>
            public static float FadeSeconds = 0.125f;

            /// <summary>How steeply the fade lets go. Above 1 it drops fast and then lingers,
            /// which reads as a glow dissolving rather than a light dimming.</summary>
            public static float FadeCurve = 1.7f;

            /// <summary>How long one point takes to come up once the front reaches it. Small
            /// enough to feel struck, large enough that the leading edge is not a hard step -
            /// and it must not outlast the head that is passing over it. When it did, the band
            /// just behind the front was still rising while the head had already left, which
            /// drew a dark notch chasing the bright one down the row. So it is tied to
            /// PropagationSeconds: speed the sweep up and this must come down with it.</summary>
            public static float Attack = 0.010f;

            /// <summary>Extra brightness carried by the moving front itself, over what the line
            /// behind it settles at. This is the whole sense of momentum: without it the row
            /// merely fills in.</summary>
            public static float HeadBoost = 1.15f;

            /// <summary>How much of the half-span the bright head is spread over.</summary>
            public static float HeadWidth = 0.17f;

            /// <summary>How much wider the beam is where the head is standing - the front pushes
            /// the profile out as it passes and it settles back behind.</summary>
            public static float HeadSwell = 0.70f;

            /// <summary>Strength at the middle and at the very ends. The ends are quieter, so the
            /// line has a direction to it and the burst in the middle stays the loudest thing on
            /// screen.</summary>
            public static float CentreIntensity = 1f;

            public static float EdgeIntensity = 0.50f;

            /// <summary>How much the beam narrows on its way to the ends, as a fraction. A beam
            /// of constant thickness is a rectangle however softly it is drawn.</summary>
            public static float EndTaper = 0.50f;

            /// <summary>How much the silhouette wanders along the row, as a fraction of its
            /// width. This is the one thing standing between "energy" and "ruler" - small enough
            /// to be felt rather than seen.</summary>
            public static float EdgeWaver = 0.13f;

            /// <summary>How many wobbles the waver fits across the line.</summary>
            public static float WaverScale = 5.5f;

            /// <summary>How far the beam's own centre line wanders off the row's, as a fraction
            /// of the aura's half-width. Tiny: enough to break the top-to-bottom mirror, not
            /// enough for the beam to look like it missed the row.</summary>
            public static float CentreDrift = 0.07f;

            /// <summary>What the line keeps behind the front, before the peak. Below 1 the row
            /// trails off behind the head instead of latching on at full strength.</summary>
            public static float TrailAmount = 0.55f;

            /// <summary>Overall opacity ceiling. The board underneath has to stay readable.</summary>
            public static float Opacity = 0.92f;

            /// <summary>Where along the energy ramp the colour starts being driven to white. Low
            /// values bleach the whole beam; this keeps white for the core alone.</summary>
            public static float WhitePoint = 0.42f;

            /// <summary>How dark the outermost haze is taken, so the aura reads as the colour's
            /// own shadow rather than a grey wash.</summary>
            public static float AuraShade = 0.22f;

            /// <summary>The per-cell answer: how strong its slot lights, and for how long. Short
            /// by design - it is there to tie the beam to the grid, not to be looked at.</summary>
            public static float CellReactionStrength = 0.62f;

            public static float CellReactionSeconds = 0.106f;

            /// <summary>How much a reacting cell swells, as a fraction of its size.</summary>
            public static float CellSwell = 0.11f;

            /// <summary>Texture resolution along the row and across it. The cross axis is the one
            /// that matters - it is what the core's thin band is drawn into.</summary>
            public static int Resolution = 288;

            public static int CrossPixels = 96;
        }

        /// <summary>Under CellFlashFx (10) and the drawn burst (11), over the board and its
        /// markers: this is the thing the explosion is standing ON.</summary>
        private const int BeamOrder = 8;

        private const int CellOrder = 9;

        // =================================================================== shared art

        /// <summary>The vertical falloff, sampled once. Every layer is this same curve at a
        /// different width, which is what keeps core, glow and aura reading as one object
        /// instead of three rings.</summary>
        private static float[] profile;

        private const int ProfileSteps = 160;

        /// <summary>How far out the profile is sampled, in layer half-widths.</summary>
        private const float ProfileReach = 1.85f;

        private static Sprite cellSprite;

        private static float[] Profile()
        {
            if (profile != null)
            {
                return profile;
            }
            profile = new float[ProfileSteps + 1];
            for (int i = 0; i <= ProfileSteps; i++)
            {
                float r = (float)i / ProfileSteps * ProfileReach;
                // Gaussian rather than a linear ramp: a straight falloff leaves a visible
                // shoulder where it meets zero, and a shoulder running the length of the row is
                // the hard edge this whole effect exists to avoid.
                profile[i] = Mathf.Exp(-2.6f * r * r);
            }
            return profile;
        }

        private static float Falloff(float r)
        {
            if (r >= ProfileReach)
            {
                return 0f;
            }
            float[] p = Profile();
            float t = r / ProfileReach * ProfileSteps;
            int i = (int)t;
            if (i >= ProfileSteps)
            {
                return p[ProfileSteps];
            }
            return Mathf.Lerp(p[i], p[i + 1], t - i);
        }

        /// <summary>One cell's slot marker: a rounded outline with a faint fill. Drawn once and
        /// shared, tinted per cell - the cells differ in TIMING, never in art.</summary>
        private static Sprite CellSprite()
        {
            if (cellSprite != null)
            {
                return cellSprite;
            }
            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            tex.hideFlags = HideFlags.HideAndDontSave;
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            var px = new Color32[n * n];
            const float half = 0.5f;
            const float radius = 0.17f;
            const float ringAt = 0.055f;   // how far in from the edge the outline sits
            const float ringHalf = 0.045f; // half its thickness
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float u = (x + 0.5f) / n - 0.5f;
                    float v = (y + 0.5f) / n - 0.5f;
                    // Signed distance to a rounded square: negative inside, zero on the edge.
                    float dx = Mathf.Abs(u) - (half - radius);
                    float dy = Mathf.Abs(v) - (half - radius);
                    float outside = Mathf.Sqrt(Mathf.Max(dx, 0f) * Mathf.Max(dx, 0f)
                        + Mathf.Max(dy, 0f) * Mathf.Max(dy, 0f));
                    float sd = outside + Mathf.Min(Mathf.Max(dx, dy), 0f) - radius;

                    float ring = 1f - Mathf.Clamp01(Mathf.Abs(sd + ringAt) / ringHalf);
                    ring *= ring;
                    // A fill that is strongest at the slot's own edge and almost nothing in the
                    // middle, so a cube sitting in the cell is still legible through it.
                    float fill = sd < 0f ? Mathf.Clamp01(1f + sd / 0.34f) * 0.30f : 0f;
                    float a = Mathf.Clamp01(ring + fill);
                    byte b = (byte)Mathf.RoundToInt(a * 255f);
                    px[y * n + x] = new Color32(255, 255, 255, b);
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false, false);
            cellSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
            return cellSprite;
        }

        // =================================================================== noise

        private static float Hash(int n)
        {
            n = (n << 13) ^ n;
            return 1f - ((n * (n * n * 15731 + 789221) + 1376312589) & 0x7fffffff)
                / 1073741824f;
        }

        /// <summary>Smooth value noise in one dimension. Deterministic from the seed, so a sweep
        /// looks the same every time it is replayed in the Animation Lab and two sweeps of the
        /// same turn - a row and a column - still differ.</summary>
        private static float Waver(float x, int seed)
        {
            int i = Mathf.FloorToInt(x);
            float f = x - i;
            f = f * f * (3f - 2f * f);
            float a = Hash(i + seed * 7919);
            float b = Hash(i + 1 + seed * 7919);
            return Mathf.Lerp(a, b, f);
        }

        // =================================================================== one live sweep

        private sealed class Sweep
        {
            public SpriteRenderer Beam;
            public Texture2D Tex;
            public Color32[] Pixels;
            public SpriteRenderer[] Cells;
            public float[] CellDistance;   // 0 at the middle of the line, 1 at its ends
            public int CellCount;
            public Color Tone;
            public float Clock;
            public float CellSize;
            public int Seed;
            public bool Running;
            public bool Frozen;            // past the peak: shape held, only the tint moves
        }

        private readonly List<Sweep> sweeps = new List<Sweep>();

        private static int seedCounter;

        /// <summary>
        /// Runs the sweep along one cleared line. <paramref name="cells"/> are world centres in
        /// board order and need not be contiguous - an eroded line still sweeps from its own
        /// middle to its own ends. <paramref name="horizontal"/> is the line's direction; a
        /// column is the same beam turned a quarter turn. <paramref name="tone"/> is the colour
        /// the destruction already has, and the only thing that decides how this looks.
        /// </summary>
        public void Play(IReadOnlyList<Vector2> cells, float cellSize, bool horizontal,
            Color tone)
        {
            if (cells == null || cells.Count == 0 || cellSize <= 0f)
            {
                return;
            }

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
            float span = max - min + cellSize;
            float alongMid = (min + max) * 0.5f;
            float across = acrossSum / cells.Count;
            float halfSpan = Mathf.Max(span * 0.5f, 0.0001f);

            Sweep s = Take(cells.Count);
            s.Tone = tone;
            s.CellSize = cellSize;
            s.Clock = 0f;
            s.Running = true;
            s.Frozen = false;
            s.CellCount = cells.Count;
            s.Seed = ++seedCounter;

            s.Beam.transform.localPosition = horizontal
                ? new Vector3(alongMid, across, 0f)
                : new Vector3(across, alongMid, 0f);
            s.Beam.transform.localRotation = horizontal
                ? Quaternion.identity
                : Quaternion.Euler(0f, 0f, 90f);
            // Local x is always the length of the line and local y always the width of the
            // beam, and the rotation points them at the world - the same trick the drawn burst
            // uses, so the two can never disagree about which way a column runs.
            s.Beam.transform.localScale = new Vector3(
                span / Style.Resolution,
                Style.AuraWidth * 2f * cellSize / Style.CrossPixels,
                1f);
            s.Beam.color = Color.white;
            s.Beam.enabled = true;

            for (int i = 0; i < cells.Count; i++)
            {
                float along = horizontal ? cells[i].x : cells[i].y;
                s.CellDistance[i] = Mathf.Clamp01(Mathf.Abs(along - alongMid) / halfSpan);
                SpriteRenderer cr = s.Cells[i];
                cr.transform.localPosition = new Vector3(cells[i].x, cells[i].y, 0f);
                cr.transform.localScale = new Vector3(cellSize, cellSize, 1f);
                cr.color = new Color(1f, 1f, 1f, 0f);
                cr.enabled = true;
            }
            for (int i = cells.Count; i < s.Cells.Length; i++)
            {
                s.Cells[i].enabled = false;
            }

            Paint(s);
        }

        // =================================================================== the painting

        /// <summary>Repaints the beam for where the wave is NOW. Everything the effect knows how
        /// to look like is in here: what has been reached, how hard the front is hitting, how
        /// wide the profile stands and what colour that adds up to.</summary>
        private void Paint(Sweep s)
        {
            int w = Style.Resolution;
            int h = Style.CrossPixels;
            float t = s.Clock;
            float travel = Mathf.Max(Style.PropagationSeconds, 0.0001f);
            float front = t / travel;                 // where the front is, 0 middle -> 1 ends
            bool peaked = t >= travel;
            float halfCross = (h - 1) * 0.5f;

            Color tone = s.Tone;
            Color aura = Color.Lerp(tone, Color.black, Style.AuraShade);
            float layerSum = 1f + Style.GlowGain + Style.AuraGain;

            for (int x = 0; x < w; x++)
            {
                // Distance from the middle of the line, 0..1 - the beam is symmetric, so one
                // pass builds both halves.
                float p = (x + 0.5f) / w * 2f - 1f;   // -1 one end, +1 the other
                float d = Mathf.Abs(p);

                float arrival = d * travel;
                float since = t - arrival;
                if (since <= 0f)
                {
                    // Not reached yet. Clearing the column matters: the texture is reused.
                    for (int y = 0; y < h; y++)
                    {
                        s.Pixels[y * w + x] = new Color32(0, 0, 0, 0);
                    }
                    continue;
                }

                float rise = Mathf.Clamp01(since / Mathf.Max(Style.Attack, 0.0001f));
                rise = rise * rise * (3f - 2f * rise);
                // What is left BEHIND the front. A point is brightest as the wave passes it and
                // sinks toward the trail level after, so the row reads as something that swept
                // through rather than something that filled in up to where the wave got to.
                float trail = Mathf.Lerp(1f, Style.TrailAmount,
                    Mathf.Clamp01(since / travel));
                if (peaked)
                {
                    // ...and then the whole line comes back up together for the peak beat. This
                    // is the moment the row is fully active, and it is a SURGE rather than a
                    // state because the trail is what it surges out of.
                    trail = Mathf.Lerp(trail, 1f,
                        Mathf.Clamp01((t - travel) / Mathf.Max(Style.PeakHold, 0.0001f)));
                }
                float amp = Mathf.Lerp(Style.CentreIntensity, Style.EdgeIntensity, d)
                    * rise * trail;

                // The head: a bright, slightly fatter band riding at the front.
                float head = 0f;
                if (!peaked)
                {
                    float gap = Mathf.Abs(d - front) / Mathf.Max(Style.HeadWidth, 0.0001f);
                    if (gap < 1f)
                    {
                        float k = 1f - gap;
                        head = k * k;
                    }
                }

                float energy = amp + head * Style.HeadBoost;

                // The silhouette: narrower toward the ends, wandering gently, pushed out where
                // the head is standing. This is what stops it being a rectangle.
                // Signed, so the two halves waver DIFFERENTLY. Reading the noise off |d| makes
                // the beam a perfect mirror of itself, and a mirrored wobble reads as a pattern
                // rather than as energy.
                float width = 1f - Style.EndTaper * d * d;
                width *= 1f + Style.EdgeWaver
                    * (Waver(p * Style.WaverScale, s.Seed)
                        + 0.5f * Waver(p * Style.WaverScale * 2.3f + 11.3f, s.Seed + 1));
                width *= 1f + Style.HeadSwell * head;
                width = Mathf.Max(width, 0.15f);

                // The line the beam is drawn ABOUT also wanders a little. Width alone keeps the
                // thing perfectly symmetric top to bottom, which is the last thing that still
                // reads as machined.
                float drift = Style.CentreDrift
                    * Waver(p * Style.WaverScale * 0.7f + 5.1f, s.Seed + 2);

                float coreR = Style.CoreWidth / Style.AuraWidth * width;
                float glowR = Style.GlowWidth / Style.AuraWidth * width;
                float auraR = width;

                for (int y = 0; y < h; y++)
                {
                    // Position across the beam, 0 on the line, 1 at the edge of the aura.
                    float v = Mathf.Abs((y - halfCross) / halfCross - drift);
                    // Divided by what the layers come to ON the line, so the number a column
                    // carries is what actually lands in the middle of the beam. Without it every
                    // point is inflated past the white point and the whole row bleaches - which
                    // is how a beam turns back into the flat bright bar it replaced.
                    float e = energy * (Falloff(v / coreR)
                        + Style.GlowGain * Falloff(v / glowR)
                        + Style.AuraGain * Falloff(v / auraR)) / layerSum;
                    // The core's OWN brightness, kept apart from the sum. White is driven from
                    // this alone: bleach from the total and the glow whitens with the core, and
                    // then there is no core to see.
                    float hot = energy * Falloff(v / coreR);
                    if (e <= 0.004f)
                    {
                        s.Pixels[y * w + x] = new Color32(0, 0, 0, 0);
                        continue;
                    }
                    // The ramp: haze is the colour shaded down, the body is the colour itself,
                    // and only the hottest part is driven to white. Built from `tone` every
                    // time, which is the whole of this effect's colour independence.
                    float white = Mathf.Clamp01((hot - Style.WhitePoint) / 0.45f);
                    Color c = e < 0.30f
                        ? Color.Lerp(aura, tone, e / 0.30f)
                        : Color.Lerp(tone, Color.white, white * 0.92f);
                    float a = Mathf.Clamp01(e) * Style.Opacity;
                    s.Pixels[y * w + x] = new Color32(
                        (byte)Mathf.RoundToInt(Mathf.Clamp01(c.r) * 255f),
                        (byte)Mathf.RoundToInt(Mathf.Clamp01(c.g) * 255f),
                        (byte)Mathf.RoundToInt(Mathf.Clamp01(c.b) * 255f),
                        (byte)Mathf.RoundToInt(a * 255f));
                }
            }
            s.Tex.SetPixels32(s.Pixels);
            s.Tex.Apply(false, false);
        }

        // =================================================================== the clock

        private void Update()
        {
            float dt = Time.deltaTime;
            float travel = Mathf.Max(Style.PropagationSeconds, 0.0001f);
            float peakEnd = travel + Style.PeakHold;
            float life = peakEnd + Style.FadeSeconds;

            for (int i = 0; i < sweeps.Count; i++)
            {
                Sweep s = sweeps[i];
                if (!s.Running)
                {
                    continue;
                }
                s.Clock += dt;
                if (s.Clock >= life)
                {
                    Retire(s);
                    continue;
                }

                if (s.Clock < peakEnd)
                {
                    // Still moving: the shape genuinely changes, so it is repainted.
                    Paint(s);
                    s.Beam.color = Color.white;
                }
                else if (!s.Frozen)
                {
                    // The last painted frame is the peak, and the peak is what dissolves. One
                    // final repaint pins it so the fade never starts from a mid-travel shape.
                    Paint(s);
                    s.Frozen = true;
                }

                if (s.Clock >= peakEnd)
                {
                    float k = (s.Clock - peakEnd) / Mathf.Max(Style.FadeSeconds, 0.0001f);
                    float fade = Mathf.Pow(Mathf.Clamp01(1f - k), Style.FadeCurve);
                    s.Beam.color = new Color(1f, 1f, 1f, fade);
                }

                UpdateCells(s, travel);
            }
        }

        /// <summary>The grid's own answer. Each slot comes up as the front reaches it and is gone
        /// long before the beam is - it says "this cell was taken", not "look at me".</summary>
        private void UpdateCells(Sweep s, float travel)
        {
            Color lit = Color.Lerp(s.Tone, Color.white, 0.55f);
            float dur = Mathf.Max(Style.CellReactionSeconds, 0.0001f);
            for (int i = 0; i < s.CellCount; i++)
            {
                float since = s.Clock - s.CellDistance[i] * travel;
                SpriteRenderer cr = s.Cells[i];
                if (since <= 0f || since >= dur)
                {
                    cr.color = new Color(1f, 1f, 1f, 0f);
                    continue;
                }
                float k = since / dur;
                // Snap on, ease off: the strike has to land on the frame the front arrives.
                float env = k < 0.18f
                    ? k / 0.18f
                    : Mathf.Pow(1f - (k - 0.18f) / 0.82f, 1.5f);
                Color c = lit;
                c.a = env * Style.CellReactionStrength;
                cr.color = c;
                float swell = 1f + Style.CellSwell * env;
                cr.transform.localScale = new Vector3(
                    s.CellSize * swell, s.CellSize * swell, 1f);
            }
        }

        // =================================================================== pooling

        /// <summary>A spent sweep, or a new one. A plus-shaped clear needs two at once, and a
        /// board power can take several lines in a turn, so these are reused rather than
        /// overwritten - the mistake the drawn burst had to be fixed for.</summary>
        private Sweep Take(int cellCount)
        {
            for (int i = 0; i < sweeps.Count; i++)
            {
                if (!sweeps[i].Running)
                {
                    Grow(sweeps[i], cellCount);
                    return sweeps[i];
                }
            }

            var go = new GameObject("Sweep" + sweeps.Count);
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = BeamOrder;
            sr.enabled = false;

            int w = Style.Resolution;
            int h = Style.CrossPixels;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.hideFlags = HideFlags.HideAndDontSave;
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            // PPU 1 so the quad is exactly Resolution x CrossPixels units before scaling, and
            // the scale above can state the world size on each axis independently. A single
            // pixelsPerUnit cannot describe a non-square sprite - it is what once drew the
            // overtime vignette as a band across the screen.
            sr.sprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 1f);

            var made = new Sweep
            {
                Beam = sr,
                Tex = tex,
                Pixels = new Color32[w * h],
                Cells = new SpriteRenderer[0],
                CellDistance = new float[0]
            };
            sweeps.Add(made);
            Grow(made, cellCount);
            return made;
        }

        /// <summary>Makes sure a sweep owns at least this many cell markers. Boards only ever get
        /// bigger inside a run, so this settles after the first line of each size.</summary>
        private void Grow(Sweep s, int cellCount)
        {
            if (s.Cells.Length >= cellCount)
            {
                return;
            }
            var cells = new SpriteRenderer[cellCount];
            System.Array.Copy(s.Cells, cells, s.Cells.Length);
            for (int i = s.Cells.Length; i < cellCount; i++)
            {
                var go = new GameObject("Cell" + i);
                go.transform.SetParent(s.Beam.transform.parent, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = CellSprite();
                sr.sortingOrder = CellOrder;
                sr.enabled = false;
                cells[i] = sr;
            }
            s.Cells = cells;
            s.CellDistance = new float[cellCount];
        }

        private void Retire(Sweep s)
        {
            s.Running = false;
            s.Frozen = false;
            s.Beam.enabled = false;
            for (int i = 0; i < s.Cells.Length; i++)
            {
                s.Cells[i].enabled = false;
            }
        }

        /// <summary>Stops everything at once - the run ending, the board being torn down. Without
        /// this a sweep would keep painting over whatever replaced the board.</summary>
        public void Stop()
        {
            for (int i = 0; i < sweeps.Count; i++)
            {
                if (sweeps[i].Running)
                {
                    Retire(sweeps[i]);
                }
            }
        }

        /// <summary>Generated textures are not assets and nothing else will collect them.</summary>
        private void OnDestroy()
        {
            for (int i = 0; i < sweeps.Count; i++)
            {
                if (sweeps[i].Tex != null)
                {
                    Destroy(sweeps[i].Tex);
                }
            }
            sweeps.Clear();
        }
    }
}
