// PURPOSE: The CLEAN SWEEP - a cleansing wave born at the middle of the board that runs out
// through every cell and breaks on the frame. Not a bigger line clear and not a blast: the one
// event on this board that is about the WHOLE arena.
//
// THE WAVE IS NEVER DRAWN. There is no ring, no expanding circle, no bubble over the board - the
// earlier version had one and it read as a glowing shield laid on top of the arena rather than
// as anything happening INSIDE it. What exists is a radius that grows with time and is otherwise
// invisible; you know where it is only because the cells it has reached are reacting and the
// ones ahead of it are not. That is the whole design: the wave is an argument passed to the
// cells, not an object on the screen.
//
// NOTHING LEAVES THE BOARD. Every renderer here is a cell-sized sprite sitting on a cell, or the
// frame itself. There is no full-board quad and no overscan, so the effect cannot spill past the
// arena however it is tuned - the failure mode of the drawn version, which had to reach beyond
// the board to break on the frame.
//
// IT REPLACED CELLS BEING PAINTED. A sweep used to go through FlashBoard -> CellFlashFx, which
// strikes each square as a flat fill at 1.02x its size. The radial schedule was already right;
// the drawing was not - a hundred and twenty squares filling in one after another is a paint
// bucket working across a grid. A cell here REACTS instead, in four beats: its slot edge takes
// the arrival, energy gathers through its inside, a small flash lands in the middle, and it is
// clean again. The cell is never filled with a flat colour at any point.
//
// THE ENERGY MOVES THROUGH THE CELL. The inner glow does not simply fade up and down in place -
// it enters on the side facing the middle of the board, travels across and gathers, which is
// what makes the wave read as passing THROUGH the square rather than as the square blinking.
// The direction is per-cell, from the board centre outward, so the whole arena carries one flow.
//
// ONE CLOCK DECIDES EVERYTHING. Cell reactions, grid, motes and the final impact all read the
// same wave position, so nothing can drift out of step. A cell answers when the front reaches
// ITS OWN distance from the middle, which is what makes the spread genuinely radial: middle
// cells, then their ring, then the edges, and the corners last, because they really are furthest.
//
// COLOUR IS AN ARGUMENT. The tone comes from the caller and the light is built from it - the
// glow is that colour, the flash is it driven to white. Nothing here knows it is blue.

using System.Collections.Generic;
using UnityEngine;
using ProjectBlock.Core;

namespace ProjectBlock.View
{
    /// <summary>The full-board cleansing wave. Fire and forget: Play, then it runs itself
    /// out.</summary>
    public sealed class BoardCleanseView : MonoBehaviour
    {
        // =================================================================== TUNING
        /// <summary>Everything that decides how the cleanse READS, in one place.</summary>
        public static class Style
        {
            // ---- the phases ----
            /// <summary>The energy gathering in the middle before anything moves. Short: it is
            /// anticipation, not an event of its own.</summary>
            public static float ChargeUpDuration = 0.15f;

            /// <summary>How long the front takes to get from the middle to the furthest CELL -
            /// the corners - so this is the whole crossing.</summary>
            public static float WaveSeconds = 0.70f;

            /// <summary>What is left after the last cell answers: the frame letting go and the
            /// last motes dying.</summary>
            public static float ReleaseSeconds = 0.45f;

            /// <summary>A little faster out of the middle than into the corners, so the wave
            /// reads as something released rather than something driven.</summary>
            public static float WaveEase = 0.78f;

            // ---- the charge ----
            /// <summary>How big the gathering core gets, in CELLS. Compact on purpose: a big
            /// sphere in the middle of the board is the drawn shockwave coming back in another
            /// form.</summary>
            public static float ChargeSize = 0.85f;

            public static float ChargeIntensity = 0.85f;

            // ---- what a cell does when the front reaches it ----
            /// <summary>One cell's whole reaction. LONG compared to the arrival beats inside
            /// it, and that is the point: a cell that goes out as soon as the front leaves makes
            /// the wave a thin broken ring of squares, which does not read as round at all. Held
            /// lit and decaying, the cells behind the front stay on and the lit region is a DISC
            /// that grows - and a filled circle reads as a circle where a one-cell ring does
            /// not.</summary>
            public static float CellReactionDuration = 0.50f;

            /// <summary>How much of the reaction the ARRIVAL takes - the edge, the flow through
            /// the cell and the flash. The rest is the cell holding and letting go, which is
            /// what fills the disc in behind the front.</summary>
            public static float CellArrivalFraction = 0.32f;

            /// <summary>What a cell settles back to after its arrival, as a fraction of its peak.
            /// This is the brightness of the filled part of the disc, and it must stay well under
            /// the front or the board just floods.</summary>
            public static float CellSustainLevel = 0.42f;

            /// <summary>The energy gathering INSIDE the cell. This is the main event of a cell's
            /// answer and should stay the strongest of the three.</summary>
            public static float CellInnerGlowStrength = 0.95f;

            /// <summary>The slot edge taking the arrival - and the grid's share of the reaction,
            /// since a cell's outline IS its piece of the grid. Deliberately well under the inner
            /// glow: the grid supports the wave, it is not the show.</summary>
            public static float CellEdgeHighlightStrength = 0.45f;

            /// <summary>The small hit in the middle once the energy has gathered there.</summary>
            public static float CellMicroFlashStrength = 0.70f;

            /// <summary>Where in the reaction the micro flash lands.</summary>
            public static float CellFlashAt = 0.56f;

            /// <summary>How far the inner glow travels across the cell, in cells. It enters on
            /// the side facing the middle of the board and settles in the centre - this is what
            /// makes the energy read as passing THROUGH rather than blinking on.</summary>
            public static float CellFlowTravel = 0.42f;

            /// <summary>How much the inner glow tightens as it gathers, as a fraction.</summary>
            public static float CellFlowGather = 0.34f;

            /// <summary>How far the slot outline oversteps the cell as the wave arrives. The
            /// whole physical reaction, and meant to be felt rather than seen.</summary>
            public static float CellScaleReaction = 0.05f;

            /// <summary>How much stronger the OUTERMOST cells answer. The wave arriving at the
            /// rim is the payoff beat, so the last ring hits harder than the middle did.</summary>
            public static float RimBoost = 0.45f;

            /// <summary>How far out a cell has to be to count as the RIM, and how hard the rim
            /// answers together when the wave completes. Needed because only the four corner
            /// cells are at distance 1 - everything else on the outer ring is nearer than that -
            /// so without this the payoff beat is four squares and a frame. This lights the whole
            /// outer band on the impact instead, which is the "outermost cells peak harder"
            /// the moment is supposed to have.</summary>
            public static float RimThreshold = 0.70f;

            public static float RimPulseStrength = 0.70f;

            public static float RimPulseSeconds = 0.22f;

            // ---- the frame ----
            /// <summary>The frame answers ONLY at the impact - it is the payoff, so it must not
            /// be lit at any other point in the effect.</summary>
            public static float FinalFrameFlashStrength = 0.55f;

            public static float FinalFrameFlashSeconds = 0.28f;

            /// <summary>How thick the frame highlight is, as a fraction of the board.</summary>
            public static float FrameWidth = 0.030f;

            // ---- the kick ----
            /// <summary>In WORLD units, which the shake turns into screen pixels through the
            /// camera: at orthographic size 5 on a 1080-tall screen a unit is 108 pixels, so this
            /// is about a 16px throw. The frequency is the falloff exponent, and lower means it
            /// rings down more slowly rather than snapping straight back - at 3.2 the whole kick
            /// was over before it registered.</summary>
            public static float ScreenShakeStrength = 0.15f;

            public static float ScreenShakeDuration = 0.17f;

            public static float ScreenShakeFrequency = 2.0f;

            // ---- the motes left behind the front ----
            /// <summary>Roughly how many cells go by per mote - a one-in-this chance rolled at
            /// each cell as the front leaves it, so they land all over the arena and somewhere
            /// different every sweep.</summary>
            public static int CellsPerMote = 3;

            public static int ImpactMotes = 12;
        }

        /// <summary>All inside the board, all over it. The inner glow sits under the outline so
        /// the slot edge always stays legible against it.</summary>
        private const int FillOrder = 6;

        private const int SlotOrder = 7;

        private const int FlashOrder = 8;

        private const int FrameOrder = 9;

        // =================================================================== shared art

        private static Sprite slotSprite;

        private static Sprite fillSprite;

        private static Sprite dotSprite;

        private static Sprite frameSprite;

        /// <summary>A cell's slot: a rounded-square outline, soft enough to glow and thin enough
        /// that it never reads as a fill. This is BOTH the cell's edge highlight and the grid's
        /// share of the reaction - a cell's outline is its piece of the grid, so lighting it
        /// lights the lines the wave is passing between, and only where the wave is.</summary>
        private static Sprite SlotSprite()
        {
            if (slotSprite != null)
            {
                return slotSprite;
            }
            slotSprite = BuildRing(72, 0.175f, 0.070f, 0.028f, 0.26f);
            return slotSprite;
        }

        /// <summary>The board's own edge, at the resolution one big object needs.</summary>
        private static Sprite FrameSprite()
        {
            if (frameSprite != null)
            {
                return frameSprite;
            }
            frameSprite = BuildRing(256, 0.045f, Style.FrameWidth * 0.5f, 0.010f, 0.022f);
            return frameSprite;
        }

        /// <summary>A rounded-square ring. `radius` is the corner rounding, `inset` how far in
        /// from the edge the line sits, `halfWidth` its thickness and `bleed` how far it fades
        /// out either side - the bleed is what makes it a glow rather than a stroke.</summary>
        private static Sprite BuildRing(int n, float radius, float inset, float halfWidth,
            float bleed)
        {
            var tex = NewTex(n);
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float sd = RoundedBox((x + 0.5f) / n - 0.5f, (y + 0.5f) / n - 0.5f, radius);
                    float off = Mathf.Abs(sd + inset);
                    float a = off <= halfWidth
                        ? 1f
                        : Mathf.Pow(Mathf.Clamp01(1f - (off - halfWidth)
                            / Mathf.Max(bleed, 0.0001f)), 2f);
                    if (sd > 0.02f)
                    {
                        a *= Mathf.Clamp01(1f - sd / 0.06f);   // nothing outside the shape
                    }
                    px[y * n + x] = new Color32(255, 255, 255,
                        (byte)Mathf.RoundToInt(Mathf.Clamp01(a) * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false, false);
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
        }

        /// <summary>The light that gathers INSIDE a cell: brightest at its middle, soft toward
        /// the slot edge, and CLIPPED to the rounded square so it can never spill into the
        /// neighbour or past the board. Not a flat fill at any point - the gradient is the whole
        /// difference between energy passing through a cell and a cell being coloured in.</summary>
        private static Sprite FillSprite()
        {
            if (fillSprite != null)
            {
                return fillSprite;
            }
            const int n = 96;
            var tex = NewTex(n);
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float u = (x + 0.5f) / n - 0.5f;
                    float v = (y + 0.5f) / n - 0.5f;
                    float sd = RoundedBox(u, v, 0.20f);
                    if (sd >= 0f)
                    {
                        px[y * n + x] = new Color32(0, 0, 0, 0);
                        continue;
                    }
                    // Radial toward the middle, then multiplied by how far inside the slot the
                    // pixel is, so the glow is held clear of the cell's own border.
                    float r = Mathf.Sqrt(u * u + v * v) / 0.5f;
                    float core = Mathf.Exp(-1.7f * r * r);
                    float inside = Mathf.Clamp01(-sd / 0.10f);
                    px[y * n + x] = new Color32(255, 255, 255,
                        (byte)Mathf.RoundToInt(Mathf.Clamp01(core * inside) * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false, false);
            fillSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
            return fillSprite;
        }

        /// <summary>The micro flash in the middle of a reacting cell, and the charge core.</summary>
        private static Sprite DotSprite()
        {
            if (dotSprite != null)
            {
                return dotSprite;
            }
            const int n = 48;
            var tex = NewTex(n);
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float u = ((x + 0.5f) / n - 0.5f) * 2f;
                    float v = ((y + 0.5f) / n - 0.5f) * 2f;
                    float a = Mathf.Exp(-4.2f * (u * u + v * v));
                    px[y * n + x] = new Color32(255, 255, 255,
                        (byte)Mathf.RoundToInt(Mathf.Clamp01(a) * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false, false);
            dotSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
            return dotSprite;
        }

        private static Texture2D NewTex(int n)
        {
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            tex.hideFlags = HideFlags.HideAndDontSave;
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            return tex;
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

        // =================================================================== state

        private SpriteRenderer charge;

        private SpriteRenderer frame;

        private SpriteRenderer[] fills = new SpriteRenderer[0];

        private SpriteRenderer[] slots = new SpriteRenderer[0];

        private SpriteRenderer[] flashes = new SpriteRenderer[0];

        /// <summary>Each cell's distance from the middle, normalised so the FURTHEST CELL is 1.
        /// This is the only thing that decides when a cell answers - and normalising on the
        /// furthest cell rather than on the board's corner is what makes the last ring land
        /// exactly at the end of the wave, which is the beat the impact belongs on.</summary>
        private float[] cellDistance = new float[0];

        /// <summary>Which way the energy travels through each cell: away from the middle.</summary>
        private Vector2[] cellFlow = new Vector2[0];

        private Vector2[] cellPos = new Vector2[0];

        private bool[] cellMoted = new bool[0];

        private int cellCount;

        private float clock;

        private bool running;

        private bool impactFired;

        private float cellSize;

        private Vector2 centre;

        private Color tone;

        private System.Action onImpact;

        private SweepSparkView motes;

        // =================================================================== driving it

        /// <summary>
        /// Sets the cleanse off over the whole arena. <paramref name="impact"/> is called once,
        /// when the last ring of cells answers, so the caller can kick the camera without this
        /// view having to know what a camera is.
        /// </summary>
        public void Play(BoardView boardView, GameBoard board, Color colour,
            SweepSparkView moteView, System.Action impact)
        {
            if (boardView == null || board == null)
            {
                return;
            }
            Rect area = boardView.WorldRect;
            if (area.width <= 0f || area.height <= 0f)
            {
                return;
            }

            tone = colour;
            motes = moteView;
            onImpact = impact;
            cellSize = boardView.CellWorldSize;
            centre = area.center;
            clock = 0f;
            running = true;
            impactFired = false;

            EnsureFurniture();
            BuildCells(boardView, board);

            charge.transform.localPosition = new Vector3(centre.x, centre.y, 0f);
            charge.color = new Color(1f, 1f, 1f, 0f);
            charge.enabled = true;

            frame.transform.localPosition = new Vector3(centre.x, centre.y, 0f);
            float frameSize = Mathf.Max(area.width, area.height) * (1f + Style.FrameWidth);
            frame.transform.localScale = new Vector3(frameSize, frameSize, 1f);
            frame.color = new Color(1f, 1f, 1f, 0f);
            frame.enabled = true;
        }

        private void EnsureFurniture()
        {
            if (charge != null)
            {
                return;
            }
            var cg = new GameObject("Charge");
            cg.transform.SetParent(transform, false);
            charge = cg.AddComponent<SpriteRenderer>();
            charge.sprite = DotSprite();
            charge.sortingOrder = FlashOrder;
            charge.enabled = false;

            var fg = new GameObject("Frame");
            fg.transform.SetParent(transform, false);
            frame = fg.AddComponent<SpriteRenderer>();
            frame.sprite = FrameSprite();
            frame.sortingOrder = FrameOrder;
            frame.enabled = false;
        }

        private void BuildCells(BoardView boardView, GameBoard board)
        {
            var live = new List<Vector2>(board.Width * board.Height);
            for (int x = 0; x < board.Width; x++)
            {
                for (int y = 0; y < board.Height; y++)
                {
                    var pos = new GridPos(board.MinX + x, board.MinY + y);
                    if (board.IsInside(pos))
                    {
                        live.Add(boardView.CellToWorld(pos));
                    }
                }
            }
            cellCount = live.Count;
            Grow(cellCount);

            float furthest = 0.0001f;
            for (int i = 0; i < cellCount; i++)
            {
                furthest = Mathf.Max(furthest, (live[i] - centre).magnitude);
            }
            for (int i = 0; i < cellCount; i++)
            {
                Vector2 off = live[i] - centre;
                cellPos[i] = live[i];
                cellDistance[i] = off.magnitude / furthest;
                cellFlow[i] = off.sqrMagnitude > 0.0001f ? off.normalized : Vector2.up;
                cellMoted[i] = false;
                var p = new Vector3(live[i].x, live[i].y, 0f);
                fills[i].transform.localPosition = p;
                slots[i].transform.localPosition = p;
                flashes[i].transform.localPosition = p;
                fills[i].color = new Color(1f, 1f, 1f, 0f);
                slots[i].color = new Color(1f, 1f, 1f, 0f);
                flashes[i].color = new Color(1f, 1f, 1f, 0f);
                fills[i].enabled = true;
                slots[i].enabled = true;
                flashes[i].enabled = true;
            }
            for (int i = cellCount; i < slots.Length; i++)
            {
                fills[i].enabled = false;
                slots[i].enabled = false;
                flashes[i].enabled = false;
            }
        }

        private void Grow(int count)
        {
            if (slots.Length >= count)
            {
                return;
            }
            var f = new SpriteRenderer[count];
            var s = new SpriteRenderer[count];
            var h = new SpriteRenderer[count];
            System.Array.Copy(fills, f, fills.Length);
            System.Array.Copy(slots, s, slots.Length);
            System.Array.Copy(flashes, h, flashes.Length);
            for (int i = slots.Length; i < count; i++)
            {
                f[i] = MakeCellRenderer("Fill" + i, FillSprite(), FillOrder);
                s[i] = MakeCellRenderer("Slot" + i, SlotSprite(), SlotOrder);
                h[i] = MakeCellRenderer("Flash" + i, DotSprite(), FlashOrder);
            }
            fills = f;
            slots = s;
            flashes = h;
            cellDistance = new float[count];
            cellFlow = new Vector2[count];
            cellPos = new Vector2[count];
            cellMoted = new bool[count];
        }

        private SpriteRenderer MakeCellRenderer(string name, Sprite sprite, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = sprite;
            r.sortingOrder = order;
            r.enabled = false;
            return r;
        }

        // =================================================================== the wave

        /// <summary>Where the front is, in normalised cell distance: below 0 while the charge is
        /// still gathering, 1 when it is on the outermost cells. Nothing draws this - it exists
        /// only to be compared against each cell's own distance.</summary>
        private float Front()
        {
            float t = clock - Style.ChargeUpDuration;
            if (t <= 0f)
            {
                return -1f;
            }
            float k = Mathf.Clamp01(t / Mathf.Max(Style.WaveSeconds, 0.0001f));
            return 1f - Mathf.Pow(1f - k, 1f / Mathf.Max(Style.WaveEase, 0.05f));
        }

        /// <summary>When the front reaches a given distance. The inverse of Front, so a cell can
        /// run its reaction on its own clock instead of tracking the wave every frame.</summary>
        private float ReachedAt(float distance)
        {
            return Style.ChargeUpDuration
                + Style.WaveSeconds * (1f - Mathf.Pow(1f - distance, Style.WaveEase));
        }

        private void Update()
        {
            if (!running)
            {
                return;
            }
            clock += Time.deltaTime;
            float life = Style.ChargeUpDuration + Style.WaveSeconds + Style.ReleaseSeconds;
            if (clock >= life)
            {
                Stop();
                return;
            }
            UpdateCharge();
            UpdateCells();
            UpdateFrame();
        }

        /// <summary>The gathering: a compact core that tightens and brightens, gone the instant
        /// the wave leaves.</summary>
        private void UpdateCharge()
        {
            if (clock >= Style.ChargeUpDuration)
            {
                charge.color = new Color(1f, 1f, 1f, 0f);
                return;
            }
            float k = Mathf.Clamp01(clock / Mathf.Max(Style.ChargeUpDuration, 0.0001f));
            Color c = Color.Lerp(tone, Color.white, 0.6f + 0.3f * k);
            c.a = Style.ChargeIntensity * k * k;
            charge.color = c;
            float s = cellSize * Style.ChargeSize * (1.5f - 0.5f * k);
            charge.transform.localScale = new Vector3(s, s, 1f);
        }

        private void UpdateCells()
        {
            float dur = Mathf.Max(Style.CellReactionDuration, 0.0001f);
            Color glow = tone;
            Color edge = Color.Lerp(tone, Color.white, 0.40f);
            Color hot = Color.Lerp(tone, Color.white, 0.80f);

            for (int i = 0; i < cellCount; i++)
            {
                float t = clock - ReachedAt(cellDistance[i]);
                if (t <= 0f || t >= dur)
                {
                    // Spent, or not reached - but the rim still answers to the impact after its
                    // own reaction is over, which is what makes the last beat a band rather than
                    // the four corners that happen to sit at distance 1.
                    float only = RimPulse(cellDistance[i]);
                    Color rg = glow;
                    rg.a = only * Style.RimPulseStrength;
                    fills[i].color = rg;
                    fills[i].transform.localPosition =
                        new Vector3(cellPos[i].x, cellPos[i].y, 0f);
                    fills[i].transform.localScale = new Vector3(cellSize, cellSize, 1f);
                    slots[i].color = new Color(1f, 1f, 1f, 0f);
                    flashes[i].color = new Color(1f, 1f, 1f, 0f);
                    continue;
                }
                float k = t / dur;
                // The outermost ring answers hardest: the wave arriving at the rim is the payoff.
                float boost = 1f + Style.RimBoost * cellDistance[i] * cellDistance[i];
                // The arrival runs on its OWN fraction of the reaction; everything after it is
                // the cell holding, which is what fills the disc in behind the front.
                float arrive = Mathf.Max(Style.CellArrivalFraction, 0.02f);
                float a = Mathf.Clamp01(k / arrive);

                // --- B: energy gathering INSIDE the cell, moving through it, then held.
                float env;
                if (k < arrive * 0.30f)
                {
                    env = k / (arrive * 0.30f);
                }
                else if (k < arrive)
                {
                    env = Mathf.Lerp(1f, Style.CellSustainLevel,
                        (k - arrive * 0.30f) / (arrive * 0.70f));
                }
                else
                {
                    env = Style.CellSustainLevel
                        * Mathf.Pow(1f - (k - arrive) / (1f - arrive), 1.5f);
                }
                Color g = glow;
                g.a = env * Style.CellInnerGlowStrength * boost;
                fills[i].color = g;
                // Enters on the side facing the middle and settles in the centre. This is the
                // "through" in "energy passing through the cell", and it belongs to the ARRIVAL
                // only - a cell that is still sliding while it holds looks like it is drifting.
                float travel = (1f - a) * Style.CellFlowTravel * cellSize;
                Vector2 at = cellPos[i] - cellFlow[i] * travel;
                fills[i].transform.localPosition = new Vector3(at.x, at.y, 0f);
                float gather = cellSize * (1f + Style.CellFlowGather * (1f - a) - 0.10f * a);
                fills[i].transform.localScale = new Vector3(gather, gather, 1f);

                // --- A: the slot edge takes the arrival, and is the grid's share of this.
                float lip = a < 0.14f ? a / 0.14f : Mathf.Pow(1f - (a - 0.14f) / 0.86f, 2.2f);
                Color e = edge;
                e.a = lip * Style.CellEdgeHighlightStrength * boost;
                slots[i].color = e;
                float ss = cellSize * (1f + Style.CellScaleReaction * lip);
                slots[i].transform.localScale = new Vector3(ss, ss, 1f);

                // --- C: the micro flash, once the energy has gathered in the middle. On the
                // arrival's clock, so it lands with the flow rather than halfway through the
                // hold.
                float d = Mathf.Abs(a - Style.CellFlashAt) / 0.22f;
                float flash = d < 1f ? (1f - d) * (1f - d) : 0f;
                Color h = hot;
                h.a = flash * Style.CellMicroFlashStrength * boost;
                flashes[i].color = h;
                float fs = cellSize * (0.22f + 0.26f * flash);
                flashes[i].transform.localScale = new Vector3(fs, fs, 1f);

                // A few motes left in the wake. Rolled rather than counted: `i % n` picks the
                // same cells every time, because the cell list is built in a fixed order.
                if (!cellMoted[i] && a > 0.4f)
                {
                    cellMoted[i] = true;
                    if (motes != null && Random.value < 1f / Mathf.Max(1, Style.CellsPerMote))
                    {
                        motes.EmitAt(cellPos[i], cellSize, 1);
                    }
                }
            }
        }

        /// <summary>How hard a cell answers the IMPACT, on top of whatever its own reaction
        /// did. Zero everywhere but the outer band, and zero until the wave completes.</summary>
        private float RimPulse(float distance)
        {
            if (!impactFired || distance < Style.RimThreshold)
            {
                return 0f;
            }
            float since = clock - (Style.ChargeUpDuration + Style.WaveSeconds);
            float k = Mathf.Clamp01(since / Mathf.Max(Style.RimPulseSeconds, 0.0001f));
            float env = k < 0.14f ? k / 0.14f : Mathf.Pow(1f - (k - 0.14f) / 0.86f, 1.8f);
            // Strongest at the very edge, easing off toward the middle of the band, so the pulse
            // has a direction rather than switching a region on.
            float depth = Mathf.InverseLerp(Style.RimThreshold, 1f, distance);
            return env * (0.45f + 0.55f * depth);
        }

        private void UpdateFrame()
        {
            // Fires when the front lands on the outermost cells - the end of the wave, which is
            // the only moment the frame is allowed to answer at all.
            if (!impactFired)
            {
                if (Front() < 1f)
                {
                    frame.color = new Color(1f, 1f, 1f, 0f);
                    return;
                }
                impactFired = true;
                if (onImpact != null)
                {
                    onImpact();
                }
                if (motes != null)
                {
                    for (int i = 0; i < Style.ImpactMotes && cellCount > 0; i++)
                    {
                        motes.EmitAt(cellPos[RimCell()], cellSize, 1);
                    }
                }
            }
            float since = clock - (Style.ChargeUpDuration + Style.WaveSeconds);
            float k = Mathf.Clamp01(since / Mathf.Max(Style.FinalFrameFlashSeconds, 0.0001f));
            float a = k < 0.10f ? k / 0.10f : Mathf.Pow(1f - (k - 0.10f) / 0.90f, 1.6f);
            Color c = Color.Lerp(tone, Color.white, 0.5f);
            c.a = a * Style.FinalFrameFlashStrength;
            frame.color = c;
        }

        /// <summary>A random cell out near the rim, for the impact's own motes.</summary>
        private int RimCell()
        {
            int best = Random.Range(0, cellCount);
            for (int i = 0; i < 6; i++)
            {
                int j = Random.Range(0, cellCount);
                if (cellDistance[j] > cellDistance[best])
                {
                    best = j;
                }
            }
            return best;
        }

        /// <summary>Stops everything at once - the run ending, the board being torn down.</summary>
        public void Stop()
        {
            running = false;
            if (charge != null)
            {
                charge.enabled = false;
            }
            if (frame != null)
            {
                frame.enabled = false;
            }
            for (int i = 0; i < slots.Length; i++)
            {
                fills[i].enabled = false;
                slots[i].enabled = false;
                flashes[i].enabled = false;
            }
        }
    }
}
