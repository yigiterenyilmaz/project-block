// PURPOSE: The overtime pressure system - the whole "critical danger" state, in one place. It is
// the conductor: it owns the clock, and it drives the board's own physical reaction and the
// screen vignette from the same beat, so the three can never drift apart.
//
// WHAT IT IS. A wave of pressure that starts OUTSIDE the arena, at its frame, and pushes inward
// through the cells a layer at a time - loading the frame, then the outer ring of cells, then the
// next, dying away towards the middle. It is not a line that travels over the board: the pulse
// that came before this one was a path-following trace, and a trace reads as decoration however
// red you make it. This reads as something pressing in on the arena from outside.
//
// LAYERS, NOT A PATH. Layer 0 is the plate's own frame; layer 1 the outermost ring of cells,
// layer 2 the ring inside that, and so on. Each layer is its own generated texture and its own
// alpha, and a pulse is those alphas firing in sequence with a delay between them. That is the
// entire mechanism, and it is why the effect is IN the board's geometry rather than over it.
//
// IT SHOWS ON THE SEAMS, never as a wash. Each layer's texture only carries the CELL BOUNDARIES
// of the cells in that layer - the grid, the slot edges, the frame's own band - so the board is
// never painted red and the blocks are never competed with. A pixel in the middle of a cell is
// left alone.
//
// THE COLOUR IS BAKED, hot inside and dirty outside. The core of a seam is amber, its halo a deep
// dark red; that gradient lives in the texture, so the runtime only ever changes ALPHA. One float
// per layer per frame is the whole per-frame cost.
//
// IT IS NOT SMOOTH, on purpose. The envelope is a hard attack, a held top and a fast decay rather
// than a sine, and the per-cell intensity is jittered so a ring never lights evenly. A clean
// even ramp reads as an animation; an uneven one with a snap on the front reads as pressure.
//
// THE ENTRY IS A SCRIPTED HIT, not the first idle pulse. Overtime has to announce itself: the
// frame charges, the board takes a squeeze, the wave runs in harder and further than an idle one,
// and the vignette snaps on and eases back. It runs under a second and then hands over to the
// loop, which is what everything after it is.

using System.Collections.Generic;
using UnityEngine;

namespace ProjectBlock.View
{
    /// <summary>The overtime pressure wave. Drives itself, the board's reaction and the vignette.</summary>
    public sealed class OvertimePressureView : MonoBehaviour
    {
        // =================================================================== TUNING
        /// <summary>Everything the effect is tuned by, in one place.</summary>
        public static class Style
        {
            // ---- colour ------------------------------------------------------------------

            /// <summary>The seam's core and its halo. Hot inside, dark and dirty outside - the
            /// two are baked into the layer textures, so nothing at runtime mixes them.</summary>
            public static Color Core = new Color(1f, 0.62f, 0.20f);

            public static Color Halo = new Color(0.55f, 0.055f, 0.045f);

            /// <summary>Width of the hot core and of the dirty halo around it, in cells.</summary>
            public static float CoreWidth = 0.045f;

            public static float HaloWidth = 0.20f;

            // ---- the wave ----------------------------------------------------------------

            /// <summary>Seconds between pulses, at the first overtime stage and the last. The
            /// brief's own numbers: noticeable pressure early, real stress late, and never fast
            /// enough to strobe.</summary>
            public static float IntervalSlow = 1.70f;

            public static float IntervalFast = 0.95f;

            /// <summary>Overtime stages the interval and the strength ramp over.</summary>
            public static int Stages = 4;

            /// <summary>Turns played in overtime per stage.</summary>
            public static int TurnsPerStage = 5;

            /// <summary>Seconds a layer waits behind the one outside it, at the first overtime
            /// stage and at the last. This IS the speed of the wave travelling inward, and the
            /// single most important number here: too small and the whole board flashes at once,
            /// too large and it reads as separate events rather than one front.
            ///
            /// It TIGHTENS with the stage, like the interval does but for a different reason: the
            /// interval is how often the pressure comes, this is how hard it hits when it does.
            /// A wave that arrives more often AND crosses the arena faster escalates on two axes
            /// at once, which is what makes late overtime feel different rather than just busier.</summary>
            public static float LayerDelaySlow = 0.085f;

            public static float LayerDelayFast = 0.038f;

            /// <summary>One layer's envelope: a hard attack, a short held top, a fast decay. Not
            /// a sine - a symmetric swell is what makes an effect look sweet.</summary>
            public static float Attack = 0.045f;

            public static float Hold = 0.055f;

            public static float Decay = 0.40f;

            /// <summary>How sharply the decay falls. Above 1 it drops fast and then lingers,
            /// which is what a struck thing does.</summary>
            public static float DecayCurve = 1.9f;

            /// <summary>What fraction of a layer's strength the next one inward gets. This is
            /// what keeps the middle of the board quiet.</summary>
            public static float InwardFalloff = 0.68f;

            /// <summary>Layers a pulse reaches at all, counting the frame - so this many minus
            /// one RINGS of cells. Everything deeper is never touched, which is what protects
            /// the middle: on a 9x9 the centre cell stays clean, on an 11x11 a 3x3 core does.</summary>
            public static int MaxLayers = 5;

            /// <summary>Peak alpha of a layer at the first stage and the last.</summary>
            public static float StrengthLow = 0.55f;

            public static float StrengthHigh = 0.95f;

            // ---- the board's own reaction ------------------------------------------------

            /// <summary>How far the board is squeezed at the peak of a pulse, and how long the
            /// squeeze takes to recover. Tiny: the player must never be able to say the board
            /// got smaller, only feel that something pressed on it.</summary>
            public static float ScaleDip = 0.006f;

            public static float ShakeAmount = 0.012f;

            /// <summary>The entry hit's own multipliers over an idle pulse.</summary>
            public static float EntryStrength = 1.35f;

            public static float EntryScaleDip = 2.6f;

            public static float EntryShake = 2.4f;

            /// <summary>How long the frame charges before the entry wave leaves it.</summary>
            public static float EntryCharge = 0.13f;

            // ---- irregularity ------------------------------------------------------------

            /// <summary>How much the per-cell intensity varies inside a layer, 0..1. Without it
            /// a ring lights as one even band and the whole thing looks printed.</summary>
            public static float CellJitter = 0.42f;

            /// <summary>Pixels per cell in the layer textures. They are all soft falloff, and
            /// there is one per layer, so this is kept modest deliberately.</summary>
            public static int Resolution = 44;
        }

        // =================================================================== internals

        /// <summary>Over the board's cells and its own surface, under the destruction flash.</summary>
        private const int SortingOrder = 6;

        private sealed class Layer
        {
            public SpriteRenderer Renderer;
            public Texture2D Texture;
        }

        private readonly List<Layer> layers = new List<Layer>();

        private BoardView board;
        private OvertimeVignetteView vignette;

        private Rect area;
        private float cellSize;
        private int cellsWide;
        private int cellsHigh;
        private int builtWide;
        private int builtHigh;
        private int stage;
        private bool running;

        /// <summary>Seconds since the current pulse started, or negative when none is running.
        /// One clock: every layer reads it with its own offset.</summary>
        private float pulseClock = -1f;

        private float pulseStrength;

        /// <summary>The layer delay this pulse is travelling at. Frozen when the pulse starts
        /// rather than read live, so a turn resolving mid-wave cannot make the front change
        /// speed halfway across the board.</summary>
        private float pulseLayerDelay;

        private bool pulseIsEntry;

        /// <summary>The continue count the vignette was last told about, so a continue lurches
        /// the ratchet exactly once rather than on every frame it is reported.</summary>
        private int lastContinues = -1;
        private float nextPulse;
        private System.Random rng = new System.Random(90210);

        /// <summary>The board and the vignette are driven from here rather than from the
        /// controller, because all three are one event and one clock.</summary>
        public void Build(BoardView boardView, OvertimeVignetteView screenVignette)
        {
            board = boardView;
            vignette = screenVignette;
        }

        /// <summary>Puts the whole overtime look away, including the board's squeeze and the
        /// vignette. Takes no arguments on purpose: it is called while the run is being torn
        /// down, when the board may already be gone and asking it for its rect would throw.</summary>
        public void Stop()
        {
            running = false;
            pulseClock = -1f;
            for (int i = 0; i < layers.Count; i++)
            {
                layers[i].Renderer.enabled = false;
            }
            if (board != null)
            {
                // Reset, or the arena would be left standing at whatever squeeze the last pulse
                // had it at - which the next round would then build on top of.
                board.SetPressure(0f, Vector2.zero);
            }
            if (vignette != null)
            {
                vignette.SetActive(false);
            }
        }

        /// <summary>Turns the pressure on or off. `continues` is the round's ContinueCount and
        /// sets the vignette's floor; `turns` is turns played past the bar and sets the rhythm.
        /// Going from off to on fires the ENTRY hit.</summary>
        public void SetState(bool on, int continues, int turnsInOvertime, Rect boardArea,
            float worldCellSize, int width, int height)
        {
            area = boardArea;
            cellSize = worldCellSize;
            cellsWide = width;
            cellsHigh = height;
            stage = Mathf.Clamp(turnsInOvertime / Mathf.Max(1, Style.TurnsPerStage),
                0, Style.Stages);

            if (vignette != null)
            {
                vignette.SetActive(on);
                if (on && continues > lastContinues)
                {
                    // Paying to carry on takes a bigger bite than a beat does.
                    vignette.Creep(OvertimeVignetteView.Style.CreepPerContinue);
                }
            }
            lastContinues = on ? continues : -1;

            if (on == running)
            {
                return;
            }
            running = on;
            if (!running)
            {
                pulseClock = -1f;
                for (int i = 0; i < layers.Count; i++)
                {
                    layers[i].Renderer.enabled = false;
                }
                if (board != null)
                {
                    board.SetPressure(0f, Vector2.zero);
                }
                return;
            }
            EnsureLayers();
            Fire(true);
        }

        private void Update()
        {
            if (!running || cellSize <= 0f || area.width <= 0f)
            {
                return;
            }
            float dt = Time.deltaTime;

            if (pulseClock >= 0f)
            {
                pulseClock += dt;
            }
            nextPulse -= dt;
            if (nextPulse <= 0f)
            {
                Fire(false);
            }

            float peak = 0f;
            for (int i = 0; i < layers.Count; i++)
            {
                float a = LayerAlpha(i);
                peak = Mathf.Max(peak, a);
                Layer l = layers[i];
                if (a <= 0.002f)
                {
                    l.Renderer.enabled = false;
                    continue;
                }
                l.Renderer.enabled = true;
                l.Renderer.color = new Color(1f, 1f, 1f, a);
            }

            // The board and the screen answer the SAME peak, so the squeeze, the shake and the
            // vignette's breath all land on the beat rather than near it.
            float hit = pulseIsEntry ? Style.EntryScaleDip : 1f;
            float shakeMul = pulseIsEntry ? Style.EntryShake : 1f;
            if (board != null)
            {
                float ang = (float)rng.NextDouble() * 6.283f;
                board.SetPressure(peak * Style.ScaleDip * hit,
                    new Vector2(Mathf.Cos(ang), Mathf.Sin(ang))
                        * peak * Style.ShakeAmount * shakeMul * cellSize);
            }
            if (vignette != null)
            {
                vignette.SetPulse(peak * (pulseIsEntry ? 1.25f : 1f));
            }
        }

        /// <summary>Starts a pulse. The entry one is stronger, reaches further and gives the
        /// frame a moment to charge before the wave leaves it.</summary>
        private void Fire(bool entry)
        {
            // The ratchet steps on the beat, which is what makes the closing something the
            // player watches happen rather than notices later.
            if (vignette != null)
            {
                vignette.Creep(OvertimeVignetteView.Style.CreepPerPulse);
            }
            pulseClock = entry ? -Style.EntryCharge : 0f;
            pulseIsEntry = entry;
            float t = Style.Stages <= 0 ? 1f : stage / (float)Style.Stages;
            pulseStrength = Mathf.Lerp(Style.StrengthLow, Style.StrengthHigh, Mathf.Clamp01(t))
                * (entry ? Style.EntryStrength : 1f);
            pulseLayerDelay = Mathf.Lerp(Style.LayerDelaySlow, Style.LayerDelayFast,
                Mathf.Clamp01(t));
            nextPulse = Mathf.Lerp(Style.IntervalSlow, Style.IntervalFast, Mathf.Clamp01(t));
            if (entry)
            {
                nextPulse += Style.EntryCharge;
            }
        }

        /// <summary>One layer's alpha right now: its own envelope, delayed by how deep it is and
        /// weakened by the same.</summary>
        private float LayerAlpha(int index)
        {
            if (pulseClock < 0f)
            {
                // The entry's charge: the frame alone, ramping, before the wave sets off.
                if (!pulseIsEntry || index != 0)
                {
                    return 0f;
                }
                float charge = 1f + pulseClock / Mathf.Max(0.0001f, Style.EntryCharge);
                return Mathf.Clamp01(charge) * pulseStrength * 0.8f;
            }
            float local = pulseClock - index * pulseLayerDelay;
            if (local < 0f)
            {
                return 0f;
            }
            float env;
            if (local < Style.Attack)
            {
                env = local / Mathf.Max(0.0001f, Style.Attack);
            }
            else if (local < Style.Attack + Style.Hold)
            {
                env = 1f;
            }
            else
            {
                float k = (local - Style.Attack - Style.Hold)
                    / Mathf.Max(0.0001f, Style.Decay);
                if (k >= 1f)
                {
                    return 0f;
                }
                env = Mathf.Pow(1f - k, Style.DecayCurve);
            }
            return env * pulseStrength * Mathf.Pow(Style.InwardFalloff, index);
        }

        // =================================================================== the layers

        /// <summary>Builds one sprite per layer for the current arena. Only when the cell counts
        /// change - the textures are drawn in cell space and do not care where the board is.</summary>
        private void EnsureLayers()
        {
            if (cellsWide <= 0 || cellsHigh <= 0)
            {
                return;
            }
            int want = Mathf.Min(Style.MaxLayers,
                1 + (Mathf.Min(cellsWide, cellsHigh) + 1) / 2);
            if (builtWide == cellsWide && builtHigh == cellsHigh && layers.Count == want)
            {
                Position();
                return;
            }
            for (int i = 0; i < layers.Count; i++)
            {
                if (layers[i].Texture != null)
                {
                    Destroy(layers[i].Texture);
                }
                Destroy(layers[i].Renderer.gameObject);
            }
            layers.Clear();
            for (int i = 0; i < want; i++)
            {
                var go = new GameObject("Layer" + i);
                go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sortingOrder = SortingOrder;
                sr.enabled = false;
                var l = new Layer { Renderer = sr };
                Bake(l, i);
                layers.Add(l);
            }
            builtWide = cellsWide;
            builtHigh = cellsHigh;
            Position();
        }

        private void Position()
        {
            float overhang = BoardView.BorderOverhang;
            float w = cellsWide * cellSize + overhang;
            float h = cellsHigh * cellSize + overhang;
            var centre = new Vector3(area.center.x, area.center.y, 0f);
            for (int i = 0; i < layers.Count; i++)
            {
                layers[i].Renderer.transform.localPosition = centre;
                layers[i].Renderer.transform.localScale = new Vector3(w, h, 1f);
            }
        }

        /// <summary>Draws one layer: the seams of the cells at that depth, or the plate's own
        /// frame band for layer 0. Colour and shape are baked; only alpha moves at runtime.</summary>
        private void Bake(Layer layer, int index)
        {
            float overhangInCells = BoardView.BorderOverhang / Mathf.Max(0.0001f, cellSize);
            float spanX = cellsWide + overhangInCells;
            float spanY = cellsHigh + overhangInCells;
            int px = Mathf.Max(8, Style.Resolution);
            int w = Mathf.Clamp(Mathf.RoundToInt(spanX * px), 32, 512);
            int h = Mathf.Clamp(Mathf.RoundToInt(spanY * px), 32, 512);

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            tex.hideFlags = HideFlags.HideAndDontSave;

            float half = overhangInCells * 0.5f;
            var pixels = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                float cy = (y + 0.5f) / h * spanY - half;
                int row = y * w;
                for (int x = 0; x < w; x++)
                {
                    float cx = (x + 0.5f) / w * spanX - half;
                    pixels[row + x] = Shade(cx, cy, index, overhangInCells);
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            layer.Texture = tex;
            layer.Renderer.sprite = Sprite.Create(tex, new Rect(0f, 0f, w, h),
                new Vector2(0.5f, 0.5f), Mathf.Max(w, h));
        }

        private Color32 Shade(float cx, float cy, int index, float overhangInCells)
        {
            float intensity;
            if (index == 0)
            {
                // The frame: a band on the plate's own edge, outside the cells entirely.
                float half = overhangInCells * 0.5f;
                float dx = Mathf.Min(cx + half, cellsWide + half - cx);
                float dy = Mathf.Min(cy + half, cellsHigh + half - cy);
                float d = Mathf.Min(dx, dy);
                if (d < 0f || d > overhangInCells + Style.HaloWidth)
                {
                    return new Color32(0, 0, 0, 0);
                }
                intensity = Profile(Mathf.Max(0f, d - overhangInCells * 0.5f));
            }
            else
            {
                int ix = Mathf.FloorToInt(cx);
                int iy = Mathf.FloorToInt(cy);
                if (ix < 0 || iy < 0 || ix >= cellsWide || iy >= cellsHigh)
                {
                    return new Color32(0, 0, 0, 0);
                }
                // Chebyshev depth: how many rings in from the arena's edge this cell is.
                int ring = Mathf.Min(Mathf.Min(ix, iy),
                    Mathf.Min(cellsWide - 1 - ix, cellsHigh - 1 - iy));
                // ONE ring per layer, and nothing deeper than the last of them. The deepest
                // cells are left out of the system entirely, which is what keeps the middle of
                // the arena clean: a last layer that mopped up every remaining ring lit the
                // whole board at the wave's midpoint, and a board lit edge to edge is a wash,
                // not a pressure front.
                if (ring != index - 1)
                {
                    return new Color32(0, 0, 0, 0);
                }
                float lx = cx - ix - 0.5f;
                float ly = cy - iy - 0.5f;
                float d = 0.5f - Mathf.Max(Mathf.Abs(lx), Mathf.Abs(ly));
                intensity = Profile(d) * CellWeight(ix, iy);
            }
            if (intensity <= 0.002f)
            {
                return new Color32(0, 0, 0, 0);
            }
            // Hot in the core, dirty in the halo. The mix is the intensity itself, so the
            // brightest part of a seam is also the hottest.
            Color c = Color.Lerp(Style.Halo, Style.Core, Mathf.Clamp01(intensity));
            return new Color32(
                (byte)Mathf.Clamp(Mathf.RoundToInt(c.r * 255f), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt(c.g * 255f), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt(c.b * 255f), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(intensity) * 255f), 0, 255));
        }

        /// <summary>The seam profile: a narrow hot core with a wider dirty shoulder. Deliberately
        /// harder than the board's own line glow - this one is meant to have an edge.</summary>
        private static float Profile(float d)
        {
            if (d < 0f)
            {
                return 0f;
            }
            float core = Mathf.Exp(-(d * d) / Mathf.Max(1e-6f, Style.CoreWidth * Style.CoreWidth));
            float halo = 0.45f
                * Mathf.Exp(-(d * d) / Mathf.Max(1e-6f, Style.HaloWidth * Style.HaloWidth));
            return Mathf.Min(1f, core + halo);
        }

        /// <summary>Per-cell variation, so a ring never lights as one even band. Hashed from the
        /// cell, so it is the same every pulse for that arena - the unevenness is meant to look
        /// like the board, not like static.</summary>
        private static float CellWeight(int ix, int iy)
        {
            uint hsh = (uint)(ix * 374761393 + iy * 668265263);
            hsh = (hsh ^ (hsh >> 13)) * 1274126177u;
            hsh ^= hsh >> 16;
            float n = (hsh % 1000u) / 1000f;
            return 1f - Style.CellJitter + Style.CellJitter * 2f * n * 0.5f;
        }

        private void OnDestroy()
        {
            for (int i = 0; i < layers.Count; i++)
            {
                if (layers[i].Texture != null)
                {
                    Destroy(layers[i].Texture);
                }
            }
        }
    }
}
