// PURPOSE: A TNT board clear, as a DETONATION rather than a colour. Short, hard and local: a
// core that goes off at the block that blew, a pressure front running out from it, cells that
// answer by distance, a handful of fast sparks, and a warm afterglow that lets go.
//
// IT REPLACED THE BOARD TURNING RED. A dynamite clear used to go through FlashBoard, which
// strikes EVERY cell on a schedule measured from the middle of the BOARD - so the whole arena
// lit the same red at once, centred on a point that had nothing to do with the bomb. That is a
// damage flash, not an explosion: there was no centre to read, no direction, and no falloff.
// Here the light is a function of distance from the TNT and dies inside a fifth of a second.
//
// THE BLAST IS NEVER DRAWN AS A RING. Same rule as the clean sweep: what exists is a radius
// that grows, and you know where it is because the cells it has passed are reacting. What IS
// drawn at the centre is a core flash - small, very bright, gone in about a tenth of a second -
// because an explosion does have a visible middle even when its pressure does not.
//
// THE CENTRE IS THE BLOCK THAT BLEW. The rule says the detonating dynamite block is the one
// PLACED THIS TURN, so report.PlacedCells is exactly where the bomb was, and no Core change was
// needed to find it. Falling back to the board's middle would put us back where we started.
//
// NOTHING MOVES THAT THE GAME CARES ABOUT. The cells react by scaling and shoving OVERLAY
// sprites of their own; the board's real squares never move. Grid geometry has to stay exactly
// where the placement code thinks it is.

using System.Collections.Generic;
using UnityEngine;
using ProjectBlock.Core;

namespace ProjectBlock.View
{
    /// <summary>The TNT detonation. Fire and forget: Play, then it runs itself out.</summary>
    public sealed class DynamiteBlastView : MonoBehaviour
    {
        // =================================================================== TUNING
        public static class Style
        {
            // ---- the beats ----
            /// <summary>The gather before it goes. Barely there on purpose - anticipation this
            /// short is felt rather than seen, and any longer reads as a fuse.</summary>
            public static float AnticipationSeconds = 0.045f;

            /// <summary>How long the core flash lasts. Very short: the flash is the moment the
            /// eye is told WHERE, and after that it is in the way.</summary>
            public static float FlashDuration = 0.085f;

            public static float FlashStrength = 1f;

            /// <summary>How big the core gets, in CELLS. Small and blinding beats big and
            /// bright - a wide flash is a screen overlay again.</summary>
            public static float FlashCoreSize = 1.15f;

            public static float FlashGlowSize = 3.2f;

            /// <summary>How far the blast reaches, in CELLS. This is what keeps it an event at a
            /// PLACE rather than a wash over the arena.</summary>
            public static float ExplosionRadius = 4.6f;

            /// <summary>How sharply the light dies with distance. Higher is more local.</summary>
            public static float LightFalloff = 2.4f;

            /// <summary>How fast the pressure front travels, in cells per second. Fast enough to
            /// cross a board in about a fifth of a second - this is a shock, not a sweep.</summary>
            public static float ShockwaveSpeed = 34f;

            public static float ShockwaveStrength = 1f;

            // ---- what a cell does when the front reaches it ----
            public static float CellImpactDuration = 0.20f;

            /// <summary>Peak brightness of a cell right at the centre, before falloff.</summary>
            public static float CellLightStrength = 0.95f;

            /// <summary>How hard a cell is shoved OUTWARD, in cells, at the centre. Tiny: this
            /// is meant to be felt as force, not seen as the grid coming apart.</summary>
            public static float CellImpactStrength = 0.085f;

            /// <summary>How much a cell swells as the front passes, as a fraction.</summary>
            public static float CellSwell = 0.13f;

            /// <summary>The slot edge's share - the grid's own answer to the pressure.</summary>
            public static float CellEdgeStrength = 0.45f;

            // ---- the kick ----
            public static float CameraShakeStrength = 0.30f;

            public static float CameraShakeDuration = 0.15f;

            /// <summary>Damping exponent. High, so it hits hard and is over - a long rumble is
            /// the cheap version of this.</summary>
            public static float CameraShakeDamping = 3.4f;

            // ---- sparks ----
            public static int SparkCount = 16;

            /// <summary>Cells per second. Far faster than the smoke, which is the whole point of
            /// having them: they carry the first impact's energy.</summary>
            public static float SparkSpeed = 13f;

            public static float SparkDrag = 0.035f;

            public static float SparkLifetime = 0.22f;

            public static float SparkLifeJitter = 0.35f;

            /// <summary>In cells. Small - these are fragments, not projectiles.</summary>
            public static float SparkSize = 0.17f;

            public static float SparkSizeJitter = 0.45f;

            // ---- afterglow ----
            public static float AfterglowStrength = 0.30f;

            public static float AfterglowSeconds = 0.34f;

            public static float AfterglowSize = 3.4f;

            // ---- palette ----
            /// <summary>The blast's own colours. Core goes to white, the body is the hot tone and
            /// the far reach is it shaded down - the caller passes the tone, so nothing here
            /// knows dynamite is red.</summary>
            public static float WhitePoint = 0.62f;

            public static float FarShade = 0.35f;
        }

        private const int GlowOrder = 12;

        private const int CellOrder = 13;

        private const int SparkOrder = 15;

        private const int CoreOrder = 16;

        // =================================================================== shared art

        private static Sprite dotSprite;

        private static Sprite slotSprite;

        /// <summary>A soft round falloff: the core, the afterglow, a cell's inner light and a
        /// spark are all this one drawing at different sizes and colours.</summary>
        private static Sprite DotSprite()
        {
            if (dotSprite != null)
            {
                return dotSprite;
            }
            const int n = 64;
            var tex = NewTex(n);
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float u = ((x + 0.5f) / n - 0.5f) * 2f;
                    float v = ((y + 0.5f) / n - 0.5f) * 2f;
                    float a = Mathf.Exp(-3.4f * (u * u + v * v));
                    px[y * n + x] = new Color32(255, 255, 255,
                        (byte)Mathf.RoundToInt(Mathf.Clamp01(a) * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false, false);
            dotSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
            return dotSprite;
        }

        /// <summary>A cell's slot outline - the grid's share of the shock.</summary>
        private static Sprite SlotSprite()
        {
            if (slotSprite != null)
            {
                return slotSprite;
            }
            const int n = 72;
            var tex = NewTex(n);
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float u = (x + 0.5f) / n - 0.5f;
                    float v = (y + 0.5f) / n - 0.5f;
                    float dx = Mathf.Abs(u) - 0.325f;
                    float dy = Mathf.Abs(v) - 0.325f;
                    float outside = Mathf.Sqrt(Mathf.Max(dx, 0f) * Mathf.Max(dx, 0f)
                        + Mathf.Max(dy, 0f) * Mathf.Max(dy, 0f));
                    float sd = outside + Mathf.Min(Mathf.Max(dx, dy), 0f) - 0.175f;
                    float off = Mathf.Abs(sd + 0.070f);
                    float a = off <= 0.028f
                        ? 1f
                        : Mathf.Pow(Mathf.Clamp01(1f - (off - 0.028f) / 0.26f), 2f);
                    if (sd > 0.02f)
                    {
                        a *= Mathf.Clamp01(1f - sd / 0.06f);
                    }
                    px[y * n + x] = new Color32(255, 255, 255,
                        (byte)Mathf.RoundToInt(Mathf.Clamp01(a) * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false, false);
            slotSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
            return slotSprite;
        }

        private static Texture2D NewTex(int n)
        {
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            tex.hideFlags = HideFlags.HideAndDontSave;
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            return tex;
        }

        // =================================================================== state

        private sealed class Spark
        {
            public SpriteRenderer Renderer;
            public Vector2 At;
            public Vector2 Velocity;
            public float Size;
            public float Life;
            public float Age;
        }

        private SpriteRenderer core;

        private SpriteRenderer coreGlow;

        private SpriteRenderer afterglow;

        private SpriteRenderer[] cellLights = new SpriteRenderer[0];

        private SpriteRenderer[] cellSlots = new SpriteRenderer[0];

        private Vector2[] cellPos = new Vector2[0];

        private Vector2[] cellOut = new Vector2[0];

        /// <summary>Each cell's distance from the BOMB, in cells. Everything a cell does is a
        /// function of this and of the clock - there is no per-cell state to keep in step.</summary>
        private float[] cellDistance = new float[0];

        private int cellCount;

        private readonly List<Spark> sparks = new List<Spark>();

        private float clock;

        private bool running;

        private float cellSize;

        private Vector2 centre;

        private Color tone;

        // =================================================================== driving it

        /// <summary>
        /// Sets the detonation off at <paramref name="at"/> - the world centre of the block that
        /// blew. <paramref name="onImpact"/> is called once, on the frame the core goes, so the
        /// caller can kick the camera without this view knowing what a camera is.
        /// </summary>
        public void Play(BoardView boardView, GameBoard board, Vector2 at, Color colour,
            System.Action onImpact)
        {
            if (boardView == null || board == null)
            {
                return;
            }
            centre = at;
            tone = colour;
            cellSize = boardView.CellWorldSize;
            clock = 0f;
            running = true;
            impactFired = false;
            this.onImpact = onImpact;

            BuildCells(boardView, board);
            EnsureFurniture();
            SpawnSparks();

            core.color = new Color(1f, 1f, 1f, 0f);
            coreGlow.color = new Color(1f, 1f, 1f, 0f);
            afterglow.color = new Color(1f, 1f, 1f, 0f);
            core.transform.localPosition = new Vector3(centre.x, centre.y, 0f);
            coreGlow.transform.localPosition = core.transform.localPosition;
            afterglow.transform.localPosition = core.transform.localPosition;
            core.enabled = true;
            coreGlow.enabled = true;
            afterglow.enabled = true;
        }

        private bool impactFired;

        private System.Action onImpact;

        private void EnsureFurniture()
        {
            if (core != null)
            {
                return;
            }
            coreGlow = Make("CoreGlow", DotSprite(), GlowOrder);
            afterglow = Make("Afterglow", DotSprite(), GlowOrder);
            core = Make("Core", DotSprite(), CoreOrder);
        }

        private SpriteRenderer Make(string name, Sprite sprite, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = sprite;
            r.sortingOrder = order;
            r.enabled = false;
            return r;
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
            for (int i = 0; i < cellCount; i++)
            {
                Vector2 off = live[i] - centre;
                cellPos[i] = live[i];
                cellDistance[i] = off.magnitude / Mathf.Max(cellSize, 0.0001f);
                cellOut[i] = off.sqrMagnitude > 0.0001f ? off.normalized : Vector2.up;
                cellLights[i].transform.localPosition = new Vector3(live[i].x, live[i].y, 0f);
                cellSlots[i].transform.localPosition = cellLights[i].transform.localPosition;
                cellLights[i].color = new Color(1f, 1f, 1f, 0f);
                cellSlots[i].color = new Color(1f, 1f, 1f, 0f);
                cellLights[i].enabled = true;
                cellSlots[i].enabled = true;
            }
            for (int i = cellCount; i < cellLights.Length; i++)
            {
                cellLights[i].enabled = false;
                cellSlots[i].enabled = false;
            }
        }

        private void Grow(int count)
        {
            if (cellLights.Length >= count)
            {
                return;
            }
            var lights = new SpriteRenderer[count];
            var slots = new SpriteRenderer[count];
            System.Array.Copy(cellLights, lights, cellLights.Length);
            System.Array.Copy(cellSlots, slots, cellSlots.Length);
            for (int i = cellLights.Length; i < count; i++)
            {
                lights[i] = Make("CellLight" + i, DotSprite(), CellOrder);
                slots[i] = Make("CellSlot" + i, SlotSprite(), CellOrder);
            }
            cellLights = lights;
            cellSlots = slots;
            cellDistance = new float[count];
            cellPos = new Vector2[count];
            cellOut = new Vector2[count];
        }

        private void SpawnSparks()
        {
            for (int i = 0; i < sparks.Count; i++)
            {
                sparks[i].Renderer.enabled = false;
            }
            for (int i = 0; i < Style.SparkCount; i++)
            {
                Spark s = i < sparks.Count ? sparks[i] : NewSpark();
                float ang = (i + Random.value) / Style.SparkCount * Mathf.PI * 2f;
                float speed = Style.SparkSpeed * cellSize * Random.Range(0.55f, 1f);
                s.At = centre + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang))
                    * cellSize * Random.Range(0.05f, 0.35f);
                s.Velocity = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * speed;
                s.Size = cellSize * Style.SparkSize
                    * (1f + Random.Range(-Style.SparkSizeJitter, Style.SparkSizeJitter));
                s.Life = Style.SparkLifetime
                    * (1f + Random.Range(-Style.SparkLifeJitter, Style.SparkLifeJitter));
                s.Age = 0f;
                s.Renderer.enabled = false;   // waits for the detonation beat
            }
        }

        private Spark NewSpark()
        {
            var s = new Spark { Renderer = Make("Spark" + sparks.Count, DotSprite(), SparkOrder) };
            sparks.Add(s);
            return s;
        }

        // =================================================================== the clock

        private void Update()
        {
            if (!running)
            {
                return;
            }
            clock += Time.deltaTime;
            float t = clock - Style.AnticipationSeconds;   // 0 at the detonation

            if (t >= 0f && !impactFired)
            {
                impactFired = true;
                if (onImpact != null)
                {
                    onImpact();
                }
                // The cloud is thrown on the detonation beat, from the bomb - not laid over
                // the camera, which is where the old smoke came from.
                SmokeFx.Burst(transform, centre, cellSize);
            }

            float life = Style.AnticipationSeconds + Style.AfterglowSeconds + 0.30f;
            if (clock >= life && AllSparksSpent(t))
            {
                Stop();
                return;
            }

            UpdateCore(t);
            UpdateCells(t);
            UpdateSparks(t);
        }

        private bool AllSparksSpent(float t)
        {
            for (int i = 0; i < sparks.Count; i++)
            {
                if (sparks[i].Renderer.enabled)
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>The gather, the flash and what is left glowing after it.</summary>
        private void UpdateCore(float t)
        {
            if (t < 0f)
            {
                // Anticipation: a point of light tightening where the bomb is.
                float k = Mathf.Clamp01(1f + t / Mathf.Max(Style.AnticipationSeconds, 0.0001f));
                Color c = Color.Lerp(tone, Color.white, 0.5f);
                c.a = 0.55f * k * k;
                core.color = c;
                float s = cellSize * Style.FlashCoreSize * (0.75f - 0.35f * k);
                core.transform.localScale = new Vector3(s, s, 1f);
                coreGlow.color = new Color(1f, 1f, 1f, 0f);
                afterglow.color = new Color(1f, 1f, 1f, 0f);
                return;
            }

            float f = Mathf.Clamp01(t / Mathf.Max(Style.FlashDuration, 0.0001f));
            if (f < 1f)
            {
                // The flash: full white the instant it goes, then straight down. The core grows
                // a little as it dies, which is what makes it read as released rather than
                // switched off.
                float env = Mathf.Pow(1f - f, 2.2f);
                var white = new Color(1f, 1f, 1f, env * Style.FlashStrength);
                core.color = white;
                float cs = cellSize * Style.FlashCoreSize * (0.55f + 1.1f * f);
                core.transform.localScale = new Vector3(cs, cs, 1f);

                Color g = Color.Lerp(tone, Color.white, 0.35f);
                g.a = env * 0.8f * Style.FlashStrength;
                coreGlow.color = g;
                float gs = cellSize * Style.FlashGlowSize * (0.35f + 1.2f * f);
                coreGlow.transform.localScale = new Vector3(gs, gs, 1f);
            }
            else
            {
                core.color = new Color(1f, 1f, 1f, 0f);
                coreGlow.color = new Color(1f, 1f, 1f, 0f);
            }

            // Afterglow: the last of the heat, low and soft, gone inside a third of a second.
            float a = Mathf.Clamp01(t / Mathf.Max(Style.AfterglowSeconds, 0.0001f));
            Color ac = Color.Lerp(tone, Color.black, Style.FarShade);
            ac.a = Mathf.Pow(1f - a, 1.7f) * Style.AfterglowStrength;
            afterglow.color = ac;
            float asz = cellSize * Style.AfterglowSize * (0.7f + 0.5f * a);
            afterglow.transform.localScale = new Vector3(asz, asz, 1f);
        }

        /// <summary>Every cell's answer, as a function of how far it is from the bomb and when
        /// the front got to it. Nothing here is stored per cell - the falloff IS the design.</summary>
        private void UpdateCells(float t)
        {
            float dur = Mathf.Max(Style.CellImpactDuration, 0.0001f);
            float speed = Mathf.Max(Style.ShockwaveSpeed, 0.001f);
            Color hot = Color.Lerp(tone, Color.white, 0.55f);
            Color edge = Color.Lerp(tone, Color.white, 0.30f);

            for (int i = 0; i < cellCount; i++)
            {
                float d = cellDistance[i];
                // How much of the blast reaches this far at all. This one line is the whole
                // difference from the old effect: the board is not lit, a NEIGHBOURHOOD is.
                float reach = Mathf.Exp(-Style.LightFalloff
                    * (d / Style.ExplosionRadius) * (d / Style.ExplosionRadius));
                if (reach < 0.02f)
                {
                    cellLights[i].color = new Color(1f, 1f, 1f, 0f);
                    cellSlots[i].color = new Color(1f, 1f, 1f, 0f);
                    continue;
                }
                float since = t - d / speed;
                if (since <= 0f || since >= dur)
                {
                    cellLights[i].color = new Color(1f, 1f, 1f, 0f);
                    cellSlots[i].color = new Color(1f, 1f, 1f, 0f);
                    cellLights[i].transform.localPosition =
                        new Vector3(cellPos[i].x, cellPos[i].y, 0f);
                    cellSlots[i].transform.localPosition =
                        cellLights[i].transform.localPosition;
                    continue;
                }
                float k = since / dur;
                // Struck, then straight down: a cell is hit, it is not lit.
                float env = k < 0.12f ? k / 0.12f : Mathf.Pow(1f - (k - 0.12f) / 0.88f, 2.0f);

                Color c = hot;
                c.a = env * reach * Style.CellLightStrength;
                cellLights[i].color = c;
                float ls = cellSize * (0.72f + Style.CellSwell * env * reach * 4f);
                cellLights[i].transform.localScale = new Vector3(ls, ls, 1f);

                Color ec = edge;
                ec.a = env * reach * Style.CellEdgeStrength;
                cellSlots[i].color = ec;
                float ss = cellSize * (1f + Style.CellSwell * env * reach);
                cellSlots[i].transform.localScale = new Vector3(ss, ss, 1f);

                // Shoved outward and let straight back. The OVERLAY moves; the board does not.
                Vector2 shove = cellOut[i] * (Style.CellImpactStrength * cellSize
                    * env * reach * Style.ShockwaveStrength);
                var at = new Vector3(cellPos[i].x + shove.x, cellPos[i].y + shove.y, 0f);
                cellLights[i].transform.localPosition = at;
                cellSlots[i].transform.localPosition = at;
            }
        }

        private void UpdateSparks(float t)
        {
            if (t < 0f)
            {
                return;
            }
            float dt = Time.deltaTime;
            for (int i = 0; i < sparks.Count && i < Style.SparkCount; i++)
            {
                Spark s = sparks[i];
                if (s.Age >= s.Life)
                {
                    s.Renderer.enabled = false;
                    continue;
                }
                s.Age += dt;
                s.Velocity *= Mathf.Pow(Style.SparkDrag, dt);
                s.At += s.Velocity * dt;
                float k = Mathf.Clamp01(s.Age / s.Life);
                Color c = Color.Lerp(Color.white, tone, k * 0.8f);
                c.a = Mathf.Pow(1f - k, 1.4f);
                s.Renderer.color = c;
                float size = s.Size * (1f - 0.55f * k);
                s.Renderer.transform.localScale = new Vector3(size, size, 1f);
                s.Renderer.transform.localPosition = new Vector3(s.At.x, s.At.y, 0f);
                s.Renderer.enabled = true;
            }
        }

        /// <summary>Stops everything at once - the run ending, the board being torn down.</summary>
        public void Stop()
        {
            running = false;
            if (core != null)
            {
                core.enabled = false;
                coreGlow.enabled = false;
                afterglow.enabled = false;
            }
            for (int i = 0; i < cellLights.Length; i++)
            {
                cellLights[i].enabled = false;
                cellSlots[i].enabled = false;
            }
            for (int i = 0; i < sparks.Count; i++)
            {
                sparks[i].Renderer.enabled = false;
            }
        }
    }
}
