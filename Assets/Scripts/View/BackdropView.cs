// PURPOSE: The BACKDROP - the surface the whole game sits on. Camera-parented, so it covers
// the view at any aspect, and drawn far below everything else (negative sorting orders; the
// board's own background is 0).
//
// Everything here is generated in code. No texture, no sprite, no imported asset: four small
// tables are built in Build() and scaled into place.
//
// WHAT IT IS. A dark TURQUOISE BLUE ground with one very large, very soft pool of WHITE light
// behind the board, and nothing else. No shapes, no panels, no rules, no pattern. Earlier
// revisions tried large geometric planes here; at any opacity that keeps them visible they read
// as debug overlay or placeholder UI, so the whole idea is gone rather than turned down.
//
// The colour split is the point: the turquoise belongs to the OUTSIDE of the screen, and the
// pool is white so that it washes the colour back out of the middle. Measured, the rim sits at
// saturation 0.61 and the centre at 0.15. Tint the pool and that second axis disappears -
// everything becomes turquoise and only brightness is left to separate board from backdrop.
//
// READ THE NOTE ON LINEAR COLOUR SPACE BELOW BEFORE CHANGING ANY COLOUR HERE.
//
// FOUR LAYERS, in order:
//   1. GROUND   - the turquoise fill, with a whisper of vertical lift so it has an up and a down.
//   2. POOL     - the light behind the board. Centred on the arena, not on the screen: the
//                 board sits at world y 0.9 over an orthographic size of 5, so the pool is
//                 lifted Style.PoolHeight (0.18) above centre to sit under it.
//   3. DITHER   - see below. Invisible on purpose.
//   4. VIGNETTE - the corners fall away, which finishes the job of pulling the eye inward.
//
// THE POOL HAS NO RIM. Its falloff is a biweight kernel, (1 - d^2)^2, which reaches zero with
// a zero slope. What makes a soft light read as a "light effect" is a boundary you can find;
// this curve does not have one anywhere. Its radii are in HALF-HEIGHT units on both axes, so
// the pool keeps its shape instead of stretching into a letterbox on a wide window.
//
// LINEAR COLOUR SPACE - THE THING THAT WILL CATCH YOU. The project is m_ActiveColorSpace: 1.
// Unity takes an authored Color from sRGB to linear, blends in LINEAR, then encodes back to
// sRGB for the display, and that changes two things that matter here:
//
//   * A dark colour is much darker in linear than its numbers suggest. The ground was once
//     (0.028, 0.060, 0.073) - a tone that previews beautifully - and it reached the screen as
//     RGB(8, 15, 18). Black, for all practical purposes.
//   * Blending WHITE into it adds the same absolute amount to all three channels. When the
//     channels start out tiny, that EQUALISES them. The same ground under the pool measured
//     RGB(84, 85, 85): saturation 0.01, a dead neutral grey where a turquoise was intended.
//
// So: pick colours by what they measure on screen after linear blending, never by how the
// numbers look in the file, and never by a preview composited in gamma space.
//
// WHY THERE IS A DITHER LAYER, AND WHY IT IS NOT THE OLD GRAIN MISTAKE. A gradient this gentle
// crosses an 8-bit quantisation step only every few dozen pixels, so it renders as concentric
// BANDS around the board - measured, the widest flat run was 196px. The fix is the standard
// one, a sub-quantisation dither. The amplitude is what makes it a dither rather than texture:
// about 3 RGB in the darkest corner and 1 behind the board, against the +/-25 of the grain that
// was rightly thrown out of an earlier revision. It takes the widest flat run to 45px.
//
// It has to be a tiled, point-filtered layer at roughly one texel per screen pixel. Baking the
// same dither into the pool texture does almost nothing - bilinear filtering averages it away
// as the texture is stretched (measured: even a 1024px pool texture leaves 120px flat runs).
//
// NOTHING HERE COMPETES WITH THE GAME. Every layer sits far below sorting order 0, so nothing
// can land on top of the board. The vignette is last, above the pool, so the light fades out at
// the frame edge rather than stopping dead - but still UNDER the board, because over it, it
// would dim the cubes near the arena's corners, and a player must never mistake lighting for a
// rule.
//
// COST. Four sprites, built once. Resize only writes transforms, and only when the aspect or
// the orthographic size actually changed. Nothing allocates after startup, nothing runs
// per-frame.

using UnityEngine;

namespace ProjectBlock.View
{
    /// <summary>Camera-parented procedural background. Build once; it maintains itself.</summary>
    public sealed class BackdropView : MonoBehaviour
    {
        // =================================================================== TUNING
        /// <summary>Every value that decides how the backdrop LOOKS, in one place.</summary>
        public static class Style
        {
            /// <summary>The base background colour: a very dark TURQUOISE BLUE. Never pure
            /// #000000, which reads as a hole rather than a surface.
            ///
            /// The hue is ~198 degrees, and that number is not free. The board's own colours sit
            /// at 220-228 (BoardSurfaceView.Style.Surface / BoardView's EmptyColor), so a backdrop that wanders
            /// too far toward green throws them out of family - at a true turquoise of ~186 the
            /// board visibly reads PURPLE against it. 198 is turquoise enough to be obvious and
            /// still close enough to the board to belong with it. If this is ever pushed further
            /// toward green, the board's greys have to move with it.
            ///
            /// IT IS NOT AS DARK AS IT LOOKS ON PAPER, AND IT CANNOT BE. See the note on LINEAR
            /// COLOUR SPACE at the top of the file: an earlier version of this used
            /// (0.028, 0.060, 0.073), which is the tone this design wants, and on screen it came
            /// out as RGB(8, 15, 18) at the rim - indistinguishable from black - while the pool
            /// flattened it to RGB(84, 85, 85), a dead neutral grey. A turquoise has to be this
            /// light before linear blending leaves any of it standing.</summary>
            public static Color Ground = new Color(0.086f, 0.200f, 0.239f);

            /// <summary>How far the ground lifts at the top and drops at the bottom, as a
            /// fraction of the base. Scales all three channels together, so the hue never
            /// shifts. 0 gives a flat single-colour ground.</summary>
            public static float GradientStrength = 0.16f;

            /// <summary>The colour of the pool, before opacity. WHITE, deliberately - the
            /// turquoise belongs to the outside of the screen, and the middle is where it gets
            /// washed out of it. White is what does the washing: at PoolStrength the centre's
            /// saturation falls from the ground's 0.58 to 0.26 while the edges keep theirs, so
            /// the screen reads turquoise at the rim and near-neutral behind the board. Tinting
            /// this the same hue as the ground undoes the whole effect - the middle just becomes
            /// a brighter turquoise and the contrast that separates board from backdrop is
            /// only brightness again.</summary>
            public static Color PoolTint = Color.white;

            /// <summary>How bright the pool gets at its centre. THE dial for this backdrop: at
            /// 0.085 the middle sits about 16 RGB above the screen edge, which reads as a lit
            /// area rather than a light. Much past 0.12 and it starts to look like an effect.</summary>
            public static float PoolStrength = 0.085f;

            /// <summary>Centre of the pool as a fraction of the half-height. Matches the board:
            /// GameUiController.BoardCenter is world y 0.9 and the camera's orthographic size
            /// is 5, so the arena sits 0.18 above the middle of the screen. If the board moves,
            /// this is the one number that has to move with it.</summary>
            public static float PoolHeight = 0.18f;

            /// <summary>Pool radii, BOTH in half-height units, so it stays the same shape at any
            /// aspect. Wider than tall because the screen is.</summary>
            public static float PoolRadiusX = 1.45f;

            public static float PoolRadiusY = 1.15f;

            /// <summary>Peak ALPHA of the anti-banding dither. A plain alpha, not a count of RGB
            /// steps, because in linear space one alpha does not buy one amount of lift: the sRGB
            /// curve is steep down in the darks, so the same value shows up roughly three times
            /// stronger in a corner than behind the board. Measured at 0.0015: about 3 RGB in the
            /// darkest corner, about 1 behind the board, and the widest flat band drops from
            /// 196px to 45px. It was 0.0064 when this was calculated as though the game rendered
            /// in gamma space - that put TEN RGB of visible speckle in the corners. Zero turns it
            /// off, which is also the way to see the banding it is here to remove.</summary>
            public static float DitherStrength = 0.0015f;

            public static float VignetteStrength = 0.42f;

            /// <summary>How far out the vignette stays completely clear (0 = centre,
            /// 1 = corner). Over half the frame is untouched.</summary>
            public static float VignetteInner = 0.42f;
        }

        // =================================================================== internals

        // Sorting: all far below the board background (0), in the order they composite.
        private const int GroundOrder = -220;
        private const int PoolOrder = -216;
        private const int DitherOrder = -212;
        private const int VignetteOrder = -205;

        /// <summary>Overscan on the full-screen layers. The camera gets shaken on a big turn,
        /// and a layer that ended exactly at the frame edge would show a seam when it does.</summary>
        private const float Cover = 1.18f;

        /// <summary>Dither texels per world unit. The game is authored for 1920x1080 over an
        /// orthographic size of 5, which is 108 screen pixels per world unit - so this puts the
        /// dither at 1:1 there. Other resolutions land within a pixel either way, which is well
        /// inside what a dither tolerates.</summary>
        private const float DitherPixelsPerUnit = 108f;

        private Camera cam;
        private Transform root;
        private SpriteRenderer ground;
        private SpriteRenderer pool;
        private SpriteRenderer dither;
        private SpriteRenderer vignette;

        private float lastAspect = -1f;
        private float lastSize = -1f;

        public void Build(Camera camera)
        {
            cam = camera != null ? camera : Camera.main;
            var go = new GameObject("Backdrop");
            root = go.transform;
            root.SetParent(cam != null ? cam.transform : transform, false);
            // Behind the play field but still inside the camera's clip range. Sorting order is
            // what actually decides the draw order; this only keeps it out of the way.
            root.localPosition = new Vector3(0f, 0f, 2f);

            // The clear colour matters even with full overscan: a resize can outrun Resize() by
            // a frame. The flag is set explicitly because the scene still asks for a SKYBOX -
            // harmless today (no skybox material is assigned, so URP falls back to this colour)
            // but not something to leave resting on a fallback.
            if (cam != null)
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Style.Ground;
            }

            ground = MakeLayer("Ground", GroundTexture(), GroundOrder);

            pool = MakeLayer("Pool", PoolTexture(), PoolOrder);
            pool.color = new Color(Style.PoolTint.r, Style.PoolTint.g, Style.PoolTint.b,
                Style.PoolStrength);

            if (Style.DitherStrength > 0f)
            {
                dither = MakeTiledLayer("Dither", DitherTexture(), DitherOrder);
                dither.color = new Color(1f, 1f, 1f, Style.DitherStrength);
            }

            vignette = MakeLayer("Vignette", VignetteTexture(), VignetteOrder);
            vignette.color = new Color(1f, 1f, 1f, Style.VignetteStrength);

            Resize();
        }

        public void SetVisible(bool visible)
        {
            if (root != null)
            {
                root.gameObject.SetActive(visible);
            }
        }

        private void LateUpdate()
        {
            if (root == null || cam == null || !root.gameObject.activeSelf)
            {
                return;
            }
            // Only on a real change: the usual frame changes nothing at all.
            if (!Mathf.Approximately(cam.aspect, lastAspect)
                || !Mathf.Approximately(cam.orthographicSize, lastSize))
            {
                Resize();
            }
        }

        private void Resize()
        {
            float halfH = cam.orthographicSize;
            float halfW = halfH * cam.aspect;
            float coverW = halfW * 2f * Cover;
            float coverH = halfH * 2f * Cover;

            Fit(ground, coverW, coverH);
            Fit(vignette, coverW, coverH);

            // The pool is NOT stretched to the screen: it is sized from its own radii, in
            // half-height units on both axes, so its shape never depends on the window.
            Fit(pool, Style.PoolRadiusX * 2f * halfH, Style.PoolRadiusY * 2f * halfH);
            pool.transform.localPosition = new Vector3(0f, Style.PoolHeight * halfH, 0f);

            if (dither != null)
            {
                dither.size = new Vector2(coverW, coverH);
            }

            lastAspect = cam.aspect;
            lastSize = cam.orthographicSize;
        }

        private static void Fit(SpriteRenderer r, float coverW, float coverH)
        {
            Sprite s = r.sprite;
            r.transform.localScale = new Vector3(
                coverW / s.texture.width, coverH / s.texture.height, 1f);
        }

        // ------------------------------------------------------------------ layers

        private SpriteRenderer MakeLayer(string name, Texture2D tex, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            var sr = go.AddComponent<SpriteRenderer>();
            // pixelsPerUnit 1 -> the sprite is (w x h) world units and Fit() scales it.
            sr.sprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height),
                new Vector2(0.5f, 0.5f), 1f);
            sr.sortingOrder = order;
            return sr;
        }

        /// <summary>A layer that REPEATS rather than stretching, so its texels stay the size of
        /// screen pixels whatever the window does. FullRect is required - tiled draw mode will
        /// not use a tight mesh.</summary>
        private SpriteRenderer MakeTiledLayer(string name, Texture2D tex, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height),
                new Vector2(0.5f, 0.5f), DitherPixelsPerUnit, 0, SpriteMeshType.FullRect);
            sr.drawMode = SpriteDrawMode.Tiled;
            sr.tileMode = SpriteTileMode.Continuous;
            sr.sortingOrder = order;
            return sr;
        }

        // ------------------------------------------------------------------ textures

        /// <summary>The ground: a vertical lift toward the top, around Style.Ground. One pixel
        /// wide - nothing varies across it, and a 1xN texture costs nothing to stretch.</summary>
        private static Texture2D GroundTexture()
        {
            const int h = 256;
            Texture2D tex = NewTexture(1, h, FilterMode.Bilinear, TextureWrapMode.Clamp);
            Color top = Scale(Style.Ground, 1f + Style.GradientStrength);
            Color bottom = Scale(Style.Ground, 1f - Style.GradientStrength);
            var px = new Color[h];
            for (int y = 0; y < h; y++)
            {
                float t = y / (float)(h - 1);
                // Eased rather than linear: a straight ramp bands visibly on a dark surface.
                t = t * t * (3f - 2f * t);
                px[y] = Color.Lerp(bottom, top, t);
            }
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }

        /// <summary>The pool: a biweight falloff, (1 - d^2)^2. Full in the middle, zero at the
        /// rim AND with zero slope there, so there is no edge to find anywhere in it.</summary>
        private static Texture2D PoolTexture()
        {
            const int n = 512;
            Texture2D tex = NewTexture(n, n, FilterMode.Bilinear, TextureWrapMode.Clamp);
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
            {
                float dy = (y / (float)(n - 1) - 0.5f) * 2f;
                for (int x = 0; x < n; x++)
                {
                    float dx = (x / (float)(n - 1) - 0.5f) * 2f;
                    float k = Mathf.Clamp01(1f - (dx * dx + dy * dy));
                    px[y * n + x] = new Color(1f, 1f, 1f, k * k);
                }
            }
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }

        /// <summary>Uniform white noise in the alpha channel. One-directional on purpose: over a
        /// ground this dark, lightening a pixel costs a fraction of the alpha that darkening it
        /// would, so a dither that only ever adds is both cheaper and easier to reason about -
        /// exactly how ordered dithering works. The renderer's alpha sets the peak.</summary>
        private static Texture2D DitherTexture()
        {
            const int n = 256;
            var rng = new System.Random(20260827);
            Texture2D tex = NewTexture(n, n, FilterMode.Point, TextureWrapMode.Repeat);
            var px = new Color32[n * n];
            for (int i = 0; i < px.Length; i++)
            {
                px[i] = new Color32(255, 255, 255, (byte)rng.Next(256));
            }
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        /// <summary>Corners fall away toward black, with the middle left completely alone.</summary>
        private static Texture2D VignetteTexture()
        {
            const int n = 256;
            Texture2D tex = NewTexture(n, n, FilterMode.Bilinear, TextureWrapMode.Clamp);
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float dx = (x / (float)(n - 1) - 0.5f) * 2f;
                    float dy = (y / (float)(n - 1) - 0.5f) * 2f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy) / 1.41421f;
                    float a = Mathf.SmoothStep(0f, 1f,
                        Mathf.InverseLerp(Style.VignetteInner, 1f, d));
                    px[y * n + x] = new Color(0f, 0f, 0f, a);
                }
            }
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }

        /// <summary>Scales all three channels together, so the hue is untouched.</summary>
        private static Color Scale(Color c, float k)
        {
            return new Color(c.r * k, c.g * k, c.b * k, c.a);
        }

        private static Texture2D NewTexture(int w, int h, FilterMode filter, TextureWrapMode wrap)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.wrapMode = wrap;
            tex.filterMode = filter;
            // Generated, never inspected, and never worth serialising into a scene.
            tex.hideFlags = HideFlags.HideAndDontSave;
            return tex;
        }
    }
}
