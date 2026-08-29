// PURPOSE: The overtime vignette - the screen closing in around the arena as a round is dragged
// out. CAMERA-PARENTED, because it darkens the whole SCREEN and not the board: the HUD, the hand,
// the joker bar, everything outside the middle goes under it.
//
// THREE THINGS MOVE, and keeping them apart is the whole design:
//
//   THE OPENING RATCHETS INWARD, a step on EVERY PULSE. It never opens back out. This is the
//   spine of the effect: the screen is visibly a little further gone after each beat than it was
//   before it, so the closing in is something the player watches happen rather than something
//   they notice later. It moved only on a CONTINUE first, which meant nothing changed at all for
//   the twenty seconds between two of them - and twenty seconds of nothing is what "I could not
//   feel it" means.
//
//   Each step takes a FRACTION OF WHAT IS LEFT rather than a fixed distance, so it closes quickly
//   while there is room and slows as it runs out, and can never shut: after twenty beats it is
//   about half closed, after fifty about seven eighths, and never past the board itself. A
//   continue takes a bigger bite of the same remainder, so paying to carry on still lurches.
//
//   THE DARKNESS follows how far the opening has come rather than counting continues, so the two
//   halves of the effect cannot disagree about how deep into the round we are.
//
//   THE SQUEEZE is the flinch ON TOP of that ratchet - in further than the step, then back to it.
//   The two together are what read as a ratchet at all: a beat that only crept would be a slow
//   drift, and one that only flinched would go nowhere. It exists because darkness
//   alone runs out of room: by the last stage the edges sit at 86% and a breath can add ten
//   points before the ceiling, so the beat would stop being felt exactly when it should be worst.
//   Moving the boundary is felt at any darkness, which is why the veil is drawn OVERSCANNED - see
//   Style.Overscan.
//
// THE OPENING IS SCREEN-SHAPED AT FIRST AND BOARD-SHAPED AT THE END, with separate extents for
// the two axes. One square opening over a 16:9 screen was the first attempt and it was wrong
// twice over: at the first continue it was WIDER than the screen and darkened nothing at all -
// measured, one per cent - and it reached the top and bottom edges four continues later than the
// sides. Starting as a constant inset from all four screen edges fixes both, since every edge is
// then the same distance outside the opening; interpolating to a square lands it on the board.
//
// The board sits high on the screen (BoardCenter y is 0.9) and the opening is centred on IT
// rather than on the screen, so the arena is never eaten from one side. That leaves more room
// below the board than above it, so the closing is felt most from the sides and from below. That
// is the layout's doing rather than a choice, and it is the right way round: the hand is at the
// bottom, and going dark down there is what the last stage is supposed to mean.
//
// GENERATED, and redrawn only when the opening has actually moved a visible amount. The darkness
// and the squeeze are a colour and a scale, so a beat costs nothing.

using UnityEngine;

namespace ProjectBlock.View
{
    /// <summary>The screen-wide overtime vignette. Driven by OvertimePressureView.</summary>
    public sealed class OvertimeVignetteView : MonoBehaviour
    {
        // =================================================================== TUNING
        /// <summary>Everything that decides how the vignette LOOKS, in one place.</summary>
        public static class Style
        {
            /// <summary>The darkness the edges settle at BETWEEN pulses, at the first continue
            /// and the last. A long way up from where this started: at 0.34, with the old square
            /// opening, the first continue darkened the screen edge by one per cent, which is
            /// not a light vignette but no vignette.</summary>
            public static float BaseLow = 0.45f;

            public static float BaseHigh = 0.86f;

            /// <summary>How much darker a pulse's peak is than the settled level.</summary>
            public static float PulseBoost = 0.24f;

            /// <summary>Ceiling on base plus boost together, so no combination of stage and beat
            /// can shut the edges completely.</summary>
            public static float MaxDarkness = 0.96f;

            /// <summary>How far the opening flinches inward at a pulse's peak, as a fraction of
            /// its size. Small - it is a flinch, not a zoom - but it is what keeps the beat
            /// legible once the darkness has run out of headroom.</summary>
            public static float PulseSqueeze = 0.030f;

            /// <summary>How fast the breath eases back after a pulse, per second. Slower than the
            /// pulse's own decay: the screen relaxes after the board does.</summary>
            public static float BreathRelease = 0.9f;

            /// <summary>How far in from the screen's edges the opening starts, in world units,
            /// when overtime begins. The SAME on all four sides, which is what makes every edge
            /// darken together from the first beat.</summary>
            public static float StartInset = 1.6f;

            /// <summary>The fraction of the REMAINING distance the opening takes on each pulse,
            /// and on each continue. Fractions rather than distances: a fixed step would either
            /// slam shut in a long overtime or never arrive in a short one, and this cannot do
            /// either - it always has somewhere to go and never gets there.</summary>
            public static float CreepPerPulse = 0.040f;

            public static float CreepPerContinue = 0.18f;

            /// <summary>Half-size of the opening at the last stage, on both axes. The board is
            /// 6.5 across, so 3.25 is exactly its edge - this stops just inside, so the outermost
            /// cells are half swallowed rather than neatly framed.</summary>
            public static float EndHalf = 2.95f;

            /// <summary>How soft the edge of the opening is, in world units. Wide: a hard edge is
            /// a mask, and the effect is meant to be the light going rather than a hole.</summary>
            public static float Softness = 2.6f;

            /// <summary>Corner radius of the opening, in world units.</summary>
            public static float CornerRadius = 1.6f;

            /// <summary>How fast the opening slides to a new resting place, in world units per
            /// second. A step is small, so this only has to keep it from snapping.</summary>
            public static float CreepSpeed = 1.4f;

            /// <summary>How much bigger than the screen the veil is drawn. The squeeze scales the
            /// quad DOWN, so without room to spare a beat would peel the veil off the screen's
            /// own edges. Anything above 1 + PulseSqueeze works; this is comfortable.</summary>
            public static float Overscan = 1.30f;

            /// <summary>How far the opening has to move before the texture is redrawn, in world
            /// units. Redrawing every frame of a close is a stall for no visible gain.</summary>
            public static float RedrawStep = 0.12f;

            /// <summary>Pixels per world unit in the generated texture. Low - the image is
            /// nothing but a wide soft falloff.</summary>
            public static float Resolution = 22f;
        }

        // =================================================================== internals

        /// <summary>Above everything the camera shows. The menus live on the HUD canvas and are
        /// not sorted against this at all, so they stay clear of it.</summary>
        private const int SortingOrder = 60;

        private Camera cam;
        private Transform root;
        private SpriteRenderer veil;
        private Texture2D texture;
        private Vector2 boardCentre;

        private bool active;
        private float breath;

        /// <summary>How far the ratchet has come, 0..1, where 1 is fully closed onto the board.
        /// Every pulse and every continue takes a bite out of what is left of it; nothing ever
        /// puts it back. The darkness is read off it too.</summary>
        private float closed;

        /// <summary>The opening's half-extents: where they are now and where they are going. Two
        /// axes, because the screen is not square and the board is - see the header. Zero means
        /// "not opened yet", which LateUpdate fills in once it knows the screen.</summary>
        private Vector2 opening = Vector2.zero;

        private Vector2 builtOpening = Vector2.one * float.NaN;
        private float builtHalfH = float.NaN;

        /// <summary>The generated texture's pixel size. Kept because the sprite is made at ONE
        /// pixel per unit, so the transform's scale has to divide by it - see the note where the
        /// scale is set.</summary>
        private int texW = 1;

        private int texH = 1;

        /// <summary>Builds the (hidden) vignette under the camera. Call once.</summary>
        public void Build(Camera camera, Vector2 centreOfBoard)
        {
            cam = camera != null ? camera : Camera.main;
            boardCentre = centreOfBoard;
            if (root == null)
            {
                var go = new GameObject("OvertimeVignette");
                root = go.transform;
                root.SetParent(cam != null ? cam.transform : transform, false);
                veil = go.AddComponent<SpriteRenderer>();
                veil.sortingOrder = SortingOrder;
                veil.enabled = false;
            }
        }

        /// <summary>Turns the vignette on or off. Turning it off forgets where the ratchet had
        /// got to, so the next round opens on a clear screen.</summary>
        public void SetActive(bool on)
        {
            if (on == active)
            {
                return;
            }
            active = on;
            if (!active)
            {
                closed = 0f;
                breath = 0f;
                opening = Vector2.zero;
            }
        }

        /// <summary>Takes one bite out of what is left of the opening. Called once per pulse, and
        /// again - harder - on each continue. A fraction of the REMAINDER, so it never shuts.</summary>
        public void Creep(float fraction)
        {
            closed = Mathf.Clamp01(closed + (1f - closed) * Mathf.Clamp01(fraction));
        }

        /// <summary>The pressure system's current beat, 0..1. Raises the breath at once and lets
        /// it fall on its own, so a pulse darkens and squeezes sharply and releases slowly.</summary>
        public void SetPulse(float envelope)
        {
            breath = Mathf.Max(breath, Mathf.Clamp01(envelope));
        }

        private void LateUpdate()
        {
            if (cam == null)
            {
                // The scene can hand us nothing on the first frame; take the main camera as soon
                // as there is one rather than staying dark for the rest of the run.
                cam = Camera.main;
                if (cam == null)
                {
                    return;
                }
                if (root != null)
                {
                    root.SetParent(cam.transform, false);
                }
            }
            if (veil == null)
            {
                return;
            }
            if (!active)
            {
                veil.enabled = false;
                return;
            }

            float halfH = cam.orthographicSize;
            float halfW = halfH * cam.aspect;
            breath = Mathf.Max(0f, breath - Style.BreathRelease * Time.deltaTime);

            // Where the ratchet has got to: the same inset from every screen edge at the start,
            // the board's own square when fully closed.
            var target = new Vector2(
                Mathf.Lerp(halfW - Style.StartInset, Style.EndHalf, closed),
                Mathf.Lerp(halfH - Style.StartInset, Style.EndHalf, closed));
            if (opening == Vector2.zero)
            {
                opening = new Vector2(halfW - Style.StartInset, halfH - Style.StartInset);
            }
            opening = new Vector2(
                Mathf.MoveTowards(opening.x, target.x, Style.CreepSpeed * Time.deltaTime),
                Mathf.MoveTowards(opening.y, target.y, Style.CreepSpeed * Time.deltaTime));

            float over = Mathf.Max(1f + Style.PulseSqueeze, Style.Overscan);
            root.localPosition = new Vector3(0f, 0f, Mathf.Abs(cam.nearClipPlane) + 0.5f);

            if (texture == null
                || Vector2.Distance(builtOpening, opening) > Style.RedrawStep
                || !Mathf.Approximately(builtHalfH, halfH))
            {
                Regenerate(halfW * over, halfH * over);
                builtOpening = opening;
                builtHalfH = halfH;
            }

            // ONE PIXEL PER UNIT, so the sprite's world size IS its pixel size and the scale
            // divides it back down per axis. A single pixelsPerUnit cannot describe a non-square
            // sprite: with max(w, h) the sprite comes out 1 unit on its long side and h/w on the
            // short one, and multiplying that by the screen's height then squashes it by the same
            // h/w again. On a 16:9 screen that drew the veil 7.3 units tall instead of 13 - a
            // hard-edged band across the middle with the top and bottom left uncovered.
            float squeeze = 1f - Style.PulseSqueeze * breath;
            veil.transform.localScale = new Vector3(
                halfW * 2f * over * squeeze / Mathf.Max(1, texW),
                halfH * 2f * over * squeeze / Mathf.Max(1, texH), 1f);
            // Darkness follows the ratchet rather than a stage count, so the screen going dark
            // and the opening coming in are always the same amount of "deep into the round".
            float baseDarkness = Mathf.Lerp(Style.BaseLow, Style.BaseHigh, closed);
            veil.color = new Color(0f, 0f, 0f,
                Mathf.Min(Style.MaxDarkness, baseDarkness + Style.PulseBoost * breath));
            veil.enabled = true;
        }

        private void OnDestroy()
        {
            if (texture != null)
            {
                Destroy(texture);
                texture = null;
            }
        }

        /// <summary>Draws the veil's SHAPE over the OVERSCANNED extent: 0 inside the opening, 1
        /// past the shoulder, soft between. How dark that shape is drawn is not in here.</summary>
        private void Regenerate(float halfW, float halfH)
        {
            int w = Mathf.Clamp(Mathf.RoundToInt(halfW * 2f * Style.Resolution), 32, 512);
            int h = Mathf.Clamp(Mathf.RoundToInt(halfH * 2f * Style.Resolution), 32, 512);
            if (texture == null || texture.width != w || texture.height != h)
            {
                if (texture != null)
                {
                    Destroy(texture);
                }
                texture = new Texture2D(w, h, TextureFormat.RGBA32, false);
                texture.wrapMode = TextureWrapMode.Clamp;
                texture.filterMode = FilterMode.Bilinear;
                texture.hideFlags = HideFlags.HideAndDontSave;
            }

            float r = Mathf.Min(Style.CornerRadius, Mathf.Min(opening.x, opening.y) * 0.9f);
            var pixels = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                float wy = ((y + 0.5f) / h * 2f - 1f) * halfH - boardCentre.y;
                int row = y * w;
                for (int x = 0; x < w; x++)
                {
                    float wx = ((x + 0.5f) / w * 2f - 1f) * halfW - boardCentre.x;
                    float d = RoundedBox(wx, wy, opening.x, opening.y, r);
                    float t = Mathf.Clamp01(d / Mathf.Max(0.01f, Style.Softness));
                    float a = t * t * (3f - 2f * t);
                    pixels[row + x] = new Color32(0, 0, 0,
                        (byte)Mathf.Clamp(Mathf.RoundToInt(a * 255f), 0, 255));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            texW = w;
            texH = h;
            veil.sprite = Sprite.Create(texture, new Rect(0f, 0f, w, h),
                new Vector2(0.5f, 0.5f), 1f);
        }

        /// <summary>Signed distance to a rounded box centred on the origin; negative inside.</summary>
        private static float RoundedBox(float px, float py, float hx, float hy, float r)
        {
            float qx = Mathf.Abs(px) - (hx - r);
            float qy = Mathf.Abs(py) - (hy - r);
            float outside = Mathf.Sqrt(Mathf.Max(qx, 0f) * Mathf.Max(qx, 0f)
                + Mathf.Max(qy, 0f) * Mathf.Max(qy, 0f));
            return outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
        }
    }
}
