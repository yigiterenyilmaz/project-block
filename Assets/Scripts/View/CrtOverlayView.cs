// PURPOSE: A fullscreen "old CRT television" overlay shown while retro (tetris) mode is on.
// Pure presentation: generated scanline + vignette textures plus a faint green tint and a
// subtle flicker, parented to the camera so it always covers the view. No game logic - the
// controller calls SetVisible(RoundRules.RetroMode). Overlay-only (no URP shader), so it works
// without touching the render pipeline.

using UnityEngine;

namespace ProjectBlock.View
{
    /// <summary>Camera-parented CRT effect. Build once, then SetVisible to toggle.</summary>
    public sealed class CrtOverlayView : MonoBehaviour
    {
        private Camera cam;
        private Transform root;
        private SpriteRenderer tint;
        private SpriteRenderer scan;
        private SpriteRenderer vignette;

        private static readonly Color TintColor = new Color(0.12f, 0.30f, 0.13f);
        private const float TintBaseAlpha = 0.10f;

        public bool IsVisible
        {
            get { return root != null && root.gameObject.activeSelf; }
        }

        public void Build(Camera camera)
        {
            cam = camera != null ? camera : Camera.main;
            var go = new GameObject("Crt");
            root = go.transform;
            root.SetParent(cam != null ? cam.transform : transform, false);
            root.localPosition = new Vector3(0f, 0f, 1f); // just in front of the camera

            tint = MakeSprite("Tint", SolidTexture(TintColor), 298);
            tint.color = new Color(TintColor.r, TintColor.g, TintColor.b, TintBaseAlpha);
            scan = MakeSprite("Scanlines", ScanlineTexture(), 300);
            vignette = MakeSprite("Vignette", VignetteTexture(), 302);

            Resize();
            go.SetActive(false);
        }

        public void SetVisible(bool visible)
        {
            if (root == null)
            {
                return;
            }
            if (visible)
            {
                Resize();
            }
            root.gameObject.SetActive(visible);
        }

        private void Update()
        {
            if (!IsVisible || cam == null)
            {
                return;
            }
            Resize();
            // Subtle brightness flicker on the tint, like an unsteady CRT.
            float a = TintBaseAlpha + 0.035f * Mathf.Sin(Time.time * 22f)
                + 0.02f * Mathf.Sin(Time.time * 6.3f);
            tint.color = new Color(TintColor.r, TintColor.g, TintColor.b, Mathf.Max(0f, a));
        }

        private void Resize()
        {
            float halfH = cam.orthographicSize;
            float halfW = halfH * cam.aspect;
            float coverW = halfW * 2f * 1.06f;
            float coverH = halfH * 2f * 1.06f;
            Fit(tint, coverW, coverH);
            Fit(scan, coverW, coverH);
            Fit(vignette, coverW, coverH);
        }

        private static void Fit(SpriteRenderer r, float coverW, float coverH)
        {
            Sprite s = r.sprite;
            float texW = s.texture.width;
            float texH = s.texture.height;
            r.transform.localScale = new Vector3(coverW / texW, coverH / texH, 1f);
        }

        private SpriteRenderer MakeSprite(string name, Texture2D tex, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            var sr = go.AddComponent<SpriteRenderer>();
            // pixelsPerUnit 1 -> base sprite is (texW x texH) world units; Fit() scales to cover.
            sr.sprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height),
                new Vector2(0.5f, 0.5f), 1f);
            sr.sortingOrder = order;
            return sr;
        }

        private static Texture2D SolidTexture(Color color)
        {
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, new Color(color.r, color.g, color.b, 1f));
            tex.Apply();
            return tex;
        }

        private static Texture2D ScanlineTexture()
        {
            const int h = 256;
            var tex = new Texture2D(1, h, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;
            for (int y = 0; y < h; y++)
            {
                // a dark line every third row, transparent gaps between
                float a = (y % 3 == 0) ? 0.55f : 0f;
                tex.SetPixel(0, y, new Color(0f, 0f, 0f, a));
            }
            tex.Apply();
            return tex;
        }

        private static Texture2D VignetteTexture()
        {
            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            Vector2 center = new Vector2((n - 1) * 0.5f, (n - 1) * 0.5f);
            float maxD = center.magnitude;
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), center) / maxD; // 0 center..1 corner
                    float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 1f, d)) * 0.6f;
                    tex.SetPixel(x, y, new Color(0f, 0f, 0f, a));
                }
            }
            tex.Apply();
            return tex;
        }
    }
}
