// PURPOSE: The drawings the BLACKJACK table is made of, baked once at runtime - the casino felt,
// its light and shadow, the house's card back, a face for a card turned over, a betting chip, a
// value token and the intro's little icons. No art files: everything is a texture written here,
// so a painter can replace any of it by assigning a Sprite and nothing else changes.
//
// THE COLOURS ARE PERCEPTUAL. Every texture is created sRGB (linear: false), so a value written
// here is the value seen. The OVERLAYS (light, vignette, mood) blend in linear colour, which
// makes a pale overlay land stronger and a dark one weaker than its alpha says - the table view
// sets their alphas with that in mind (BlackjackTableView.DarkAlpha), the trap the Tılsım and
// debt passes both documented.
//
// The palette (designer's brief): deep green-petrol felt, warm amber, muted gold, burgundy,
// charcoal, cream - and never neon, never a poker-table print, never a slot machine.

using UnityEngine;

namespace ProjectBlock.View
{
    public static class BlackjackShapes
    {
        // ---- the palette ----
        public static readonly Color Felt = new Color(0.055f, 0.135f, 0.125f);
        public static readonly Color FeltDeep = new Color(0.030f, 0.075f, 0.072f);
        public static readonly Color Amber = new Color(1.00f, 0.74f, 0.38f);
        public static readonly Color Gold = new Color(0.86f, 0.70f, 0.40f);
        public static readonly Color GoldDim = new Color(0.58f, 0.46f, 0.26f);
        public static readonly Color Burgundy = new Color(0.30f, 0.055f, 0.085f);
        public static readonly Color BurgundyLight = new Color(0.52f, 0.13f, 0.17f);
        public static readonly Color Charcoal = new Color(0.075f, 0.075f, 0.085f);
        public static readonly Color Cream = new Color(0.96f, 0.91f, 0.80f);
        public static readonly Color CreamCool = new Color(0.86f, 0.88f, 0.90f);
        public static readonly Color PlayerInk = new Color(1.00f, 0.88f, 0.62f);
        public static readonly Color DealerInk = new Color(0.90f, 0.78f, 0.74f);

        private static Sprite felt;
        private static Sprite radial;
        private static Sprite vignette;
        private static Sprite topShade;
        private static Sprite cardBack;
        private static Sprite cardFace;
        private static Sprite chip;
        private static Sprite token;
        private static Sprite target;
        private static Sprite arc;
        private static Sprite triangle;

        /// <summary>A small soft triangle pointing RIGHT (the lead arrow; turn it to point).</summary>
        public static Sprite TriangleSprite
        {
            get
            {
                if (triangle == null)
                {
                    const int n = 48;
                    var tex = NewTexture(n, n, true);
                    var px = new Color32[n * n];
                    for (int y = 0; y < n; y++)
                    {
                        for (int x = 0; x < n; x++)
                        {
                            float u = (x + 0.5f) / n;
                            float v = Mathf.Abs((y + 0.5f) / n * 2f - 1f);
                            // inside when v < 1 - u (a point at the right)
                            float d = (1f - u) - v;
                            float a = Mathf.Clamp01(d * n * 0.35f) * Mathf.Clamp01(u * n * 0.35f);
                            px[y * n + x] = new Color(1f, 1f, 1f, a);
                        }
                    }
                    tex.SetPixels32(px);
                    tex.Apply();
                    triangle = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
                }
                return triangle;
            }
        }

        // ------------------------------------------------------------------ the felt

        /// <summary>The table's cloth, opaque: deep green-petrol with fine fibres and broad worn
        /// tonal drift. No light in it - the light is its own layer so it can breathe.</summary>
        public static Sprite FeltSprite
        {
            get
            {
                if (felt == null)
                {
                    const int w = 480;
                    const int h = 270;
                    var tex = NewTexture(w, h, false);
                    var px = new Color32[w * h];
                    for (int y = 0; y < h; y++)
                    {
                        for (int x = 0; x < w; x++)
                        {
                            float u = x / (float)w;
                            float v = y / (float)h;
                            // broad worn variation: two octaves of value noise
                            float broad = ValueNoise(u * 3.1f, v * 2.2f, 11) * 0.65f
                                + ValueNoise(u * 7.3f, v * 5.1f, 23) * 0.35f;
                            // fibres: fine, slightly stretched along x (the nap of the cloth)
                            float fibre = Hash(x * 3 + (y / 2) * 977, 5) * 0.6f + Hash(x, y, 9) * 0.4f;
                            float k = 1f + (broad - 0.5f) * 0.14f + (fibre - 0.5f) * 0.05f;
                            Color c = Felt * k;
                            // a breath of burgundy in the worn patches
                            c = Color.Lerp(c, Burgundy * 0.6f, Mathf.Clamp01((0.35f - broad) * 0.4f));
                            c.a = 1f;
                            px[y * w + x] = c;
                        }
                    }
                    tex.SetPixels32(px);
                    tex.Apply();
                    felt = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), w);
                }
                return felt;
            }
        }

        /// <summary>A soft round light: white, alpha falling smoothly to nothing at its own edge
        /// (a gradient that dies at its edge - never a flat disc).</summary>
        public static Sprite RadialSprite
        {
            get
            {
                if (radial == null)
                {
                    const int n = 128;
                    var tex = NewTexture(n, n, true);
                    var px = new Color32[n * n];
                    for (int y = 0; y < n; y++)
                    {
                        for (int x = 0; x < n; x++)
                        {
                            float dx = (x + 0.5f) / n * 2f - 1f;
                            float dy = (y + 0.5f) / n * 2f - 1f;
                            float d = Mathf.Sqrt(dx * dx + dy * dy);
                            float a = Mathf.Clamp01(1f - d);
                            a = a * a * (3f - 2f * a);
                            px[y * n + x] = new Color(1f, 1f, 1f, a);
                        }
                    }
                    tex.SetPixels32(px);
                    tex.Apply();
                    radial = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
                }
                return radial;
            }
        }

        /// <summary>The table's edge shadow: clear in the middle, rising toward every edge on a
        /// rounded-rectangle falloff (never a round mask - the screen is not round).</summary>
        public static Sprite VignetteSprite
        {
            get
            {
                if (vignette == null)
                {
                    const int w = 256;
                    const int h = 144;
                    var tex = NewTexture(w, h, true);
                    var px = new Color32[w * h];
                    for (int y = 0; y < h; y++)
                    {
                        for (int x = 0; x < w; x++)
                        {
                            float dx = Mathf.Abs((x + 0.5f) / w * 2f - 1f);
                            float dy = Mathf.Abs((y + 0.5f) / h * 2f - 1f);
                            float px4 = Mathf.Pow(Mathf.Pow(dx, 4f) + Mathf.Pow(dy, 4f), 0.25f);
                            float a = Mathf.Clamp01((px4 - 0.45f) / 0.6f);
                            a = a * a;
                            px[y * w + x] = new Color(1f, 1f, 1f, a);
                        }
                    }
                    tex.SetPixels32(px);
                    tex.Apply();
                    vignette = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), w);
                }
                return vignette;
            }
        }

        /// <summary>Opaque at the top, gone by the middle: the table is darker overhead.</summary>
        public static Sprite TopShadeSprite
        {
            get
            {
                if (topShade == null)
                {
                    const int w = 8;
                    const int h = 128;
                    var tex = NewTexture(w, h, true);
                    var px = new Color32[w * h];
                    for (int y = 0; y < h; y++)
                    {
                        float v = y / (float)(h - 1);
                        float a = Mathf.Clamp01((v - 0.5f) / 0.5f);
                        a = a * a;
                        for (int x = 0; x < w; x++)
                        {
                            px[y * w + x] = new Color(1f, 1f, 1f, a);
                        }
                    }
                    tex.SetPixels32(px);
                    tex.Apply();
                    topShade = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), w);
                }
                return topShade;
            }
        }

        /// <summary>A thin brass arc, faint - the only "table marking" the felt carries.</summary>
        public static Sprite ArcSprite
        {
            get
            {
                if (arc == null)
                {
                    const int w = 256;
                    const int h = 96;
                    var tex = NewTexture(w, h, true);
                    var px = new Color32[w * h];
                    for (int y = 0; y < h; y++)
                    {
                        for (int x = 0; x < w; x++)
                        {
                            float u = (x + 0.5f) / w * 2f - 1f;
                            float v = (y + 0.5f) / h;
                            // an arc of a wide ellipse, open downward
                            float curve = 0.82f - 0.62f * u * u;
                            float d = Mathf.Abs(v - curve) * h;
                            float a = Mathf.Clamp01(1.4f - d * 0.7f) * Mathf.Clamp01((1f - Mathf.Abs(u)) * 3f);
                            px[y * w + x] = new Color(1f, 1f, 1f, a);
                        }
                    }
                    tex.SetPixels32(px);
                    tex.Apply();
                    arc = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), w);
                }
                return arc;
            }
        }

        // ------------------------------------------------------------------ cards

        /// <summary>The house's card back: burgundy over deep green, a thin muted-gold rim and a
        /// gold lattice with a lozenge at its heart. No suits - this is not a playing card.</summary>
        public static Sprite CardBackSprite
        {
            get
            {
                if (cardBack == null)
                {
                    const int w = 90;
                    const int h = 120;
                    var tex = NewTexture(w, h, false);
                    var px = new Color32[w * h];
                    for (int y = 0; y < h; y++)
                    {
                        for (int x = 0; x < w; x++)
                        {
                            float inside = RoundRect(x, y, w, h, 9f);
                            if (inside <= 0f)
                            {
                                px[y * w + x] = new Color(0f, 0f, 0f, 0f);
                                continue;
                            }
                            float edge = RoundRectDistance(x, y, w, h, 9f);
                            float u = (x + 0.5f) / w - 0.5f;
                            float v = (y + 0.5f) / h - 0.5f;
                            Color c = Color.Lerp(FeltDeep * 1.4f, Burgundy, 0.55f + 0.25f * v);
                            // rim: a thin gold line inset from the edge
                            float rim = Mathf.Clamp01(1f - Mathf.Abs(edge - 5.5f) * 0.9f);
                            // lattice: diamonds inside the rim
                            float lu = (u + v) * 9f;
                            float lv = (u - v) * 9f;
                            float lattice = Mathf.Max(LineWave(lu), LineWave(lv)) * (edge > 9f ? 1f : 0f);
                            // the lozenge at the heart
                            float lozenge = Mathf.Abs(u) * 1.4f + Mathf.Abs(v);
                            float heart = Mathf.Clamp01(1f - Mathf.Abs(lozenge - 0.16f) * 60f)
                                + (lozenge < 0.1f ? 0.35f : 0f);
                            c = Color.Lerp(c, GoldDim, lattice * 0.32f);
                            c = Color.Lerp(c, Gold, Mathf.Clamp01(rim * 0.9f + heart * 0.8f));
                            // a soft top-left sheen
                            c *= 1f + 0.10f * Mathf.Clamp01(-u - v);
                            c.a = Mathf.Clamp01(inside);
                            px[y * w + x] = c;
                        }
                    }
                    tex.SetPixels32(px);
                    tex.Apply();
                    cardBack = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), w);
                }
                return cardBack;
            }
        }

        /// <summary>The face of a card the house turns over: cream with a thin warm rim. The
        /// block itself is drawn on top in its own cubes.</summary>
        public static Sprite CardFaceSprite
        {
            get
            {
                if (cardFace == null)
                {
                    const int w = 90;
                    const int h = 120;
                    var tex = NewTexture(w, h, false);
                    var px = new Color32[w * h];
                    for (int y = 0; y < h; y++)
                    {
                        for (int x = 0; x < w; x++)
                        {
                            float inside = RoundRect(x, y, w, h, 9f);
                            if (inside <= 0f)
                            {
                                px[y * w + x] = new Color(0f, 0f, 0f, 0f);
                                continue;
                            }
                            float edge = RoundRectDistance(x, y, w, h, 9f);
                            Color c = Color.Lerp(Cream * 0.86f, Cream, Mathf.Clamp01(edge / 18f));
                            float rim = Mathf.Clamp01(1f - Mathf.Abs(edge - 4.5f) * 0.8f);
                            c = Color.Lerp(c, GoldDim, rim * 0.7f);
                            c.a = Mathf.Clamp01(inside);
                            px[y * w + x] = c;
                        }
                    }
                    tex.SetPixels32(px);
                    tex.Apply();
                    cardFace = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), w);
                }
                return cardFace;
            }
        }

        // ------------------------------------------------------------------ money

        /// <summary>A stylised chip: a dark disc, a cream-and-gold rim with notches - no photoreal
        /// poker chip.</summary>
        public static Sprite ChipSprite
        {
            get
            {
                if (chip == null)
                {
                    const int n = 96;
                    var tex = NewTexture(n, n, false);
                    var px = new Color32[n * n];
                    for (int y = 0; y < n; y++)
                    {
                        for (int x = 0; x < n; x++)
                        {
                            float dx = (x + 0.5f) / n * 2f - 1f;
                            float dy = (y + 0.5f) / n * 2f - 1f;
                            float d = Mathf.Sqrt(dx * dx + dy * dy);
                            float alpha = Mathf.Clamp01((1f - d) * n * 0.5f);
                            if (alpha <= 0f)
                            {
                                px[y * n + x] = new Color(0f, 0f, 0f, 0f);
                                continue;
                            }
                            float angle = Mathf.Atan2(dy, dx);
                            Color c = Charcoal * 1.4f;
                            if (d > 0.78f)
                            {
                                // the rim: gold, broken by six cream notches
                                bool notch = Mathf.Repeat(angle / (Mathf.PI * 2f) * 6f, 1f) < 0.32f;
                                c = notch ? Cream * 0.95f : Gold;
                            }
                            else if (d > 0.70f)
                            {
                                c = Charcoal * 0.8f;
                            }
                            else if (Mathf.Abs(d - 0.56f) < 0.025f)
                            {
                                c = GoldDim; // the inner ring the value sits in
                            }
                            c *= 1f + 0.12f * Mathf.Clamp01(-dx * 0.7f + dy * 0.7f);
                            c.a = alpha;
                            px[y * n + x] = c;
                        }
                    }
                    tex.SetPixels32(px);
                    tex.Apply();
                    chip = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
                }
                return chip;
            }
        }

        /// <summary>A value token in flight: a small gold lozenge with a lit edge.</summary>
        public static Sprite TokenSprite
        {
            get
            {
                if (token == null)
                {
                    const int n = 48;
                    var tex = NewTexture(n, n, false);
                    var px = new Color32[n * n];
                    for (int y = 0; y < n; y++)
                    {
                        for (int x = 0; x < n; x++)
                        {
                            float dx = Mathf.Abs((x + 0.5f) / n * 2f - 1f);
                            float dy = Mathf.Abs((y + 0.5f) / n * 2f - 1f);
                            float l = dx * 1.25f + dy;
                            float alpha = Mathf.Clamp01((0.92f - l) * n * 0.4f);
                            Color c = Color.Lerp(Gold, Cream, Mathf.Clamp01(1f - l * 1.6f) * 0.7f);
                            c.a = alpha;
                            px[y * n + x] = c;
                        }
                    }
                    tex.SetPixels32(px);
                    tex.Apply();
                    token = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
                }
                return token;
            }
        }

        /// <summary>The target icon: two thin rings and a dot.</summary>
        public static Sprite TargetSprite
        {
            get
            {
                if (target == null)
                {
                    const int n = 64;
                    var tex = NewTexture(n, n, true);
                    var px = new Color32[n * n];
                    for (int y = 0; y < n; y++)
                    {
                        for (int x = 0; x < n; x++)
                        {
                            float dx = (x + 0.5f) / n * 2f - 1f;
                            float dy = (y + 0.5f) / n * 2f - 1f;
                            float d = Mathf.Sqrt(dx * dx + dy * dy);
                            float a = Mathf.Clamp01(1f - Mathf.Abs(d - 0.82f) * 14f)
                                + Mathf.Clamp01(1f - Mathf.Abs(d - 0.48f) * 14f)
                                + Mathf.Clamp01((0.18f - d) * 30f);
                            px[y * n + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(a));
                        }
                    }
                    tex.SetPixels32(px);
                    tex.Apply();
                    target = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
                }
                return target;
            }
        }

        // ------------------------------------------------------------------ helpers

        private static Texture2D NewTexture(int w, int h, bool alphaOnly)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            tex.hideFlags = HideFlags.HideAndDontSave;
            return tex;
        }

        /// <summary>Coverage of a rounded rectangle at a pixel (antialiased over one pixel).</summary>
        private static float RoundRect(int x, int y, int w, int h, float r)
        {
            return Mathf.Clamp01(RoundRectDistance(x, y, w, h, r) + 0.5f);
        }

        /// <summary>Distance INSIDE a rounded rectangle's edge, in pixels (negative outside).</summary>
        private static float RoundRectDistance(int x, int y, int w, int h, float r)
        {
            float px = x + 0.5f;
            float py = y + 0.5f;
            float qx = Mathf.Abs(px - w * 0.5f) - (w * 0.5f - r);
            float qy = Mathf.Abs(py - h * 0.5f) - (h * 0.5f - r);
            float outside = Mathf.Sqrt(Mathf.Max(qx, 0f) * Mathf.Max(qx, 0f) + Mathf.Max(qy, 0f) * Mathf.Max(qy, 0f))
                + Mathf.Min(Mathf.Max(qx, qy), 0f);
            return r - outside;
        }

        private static float LineWave(float t)
        {
            float f = Mathf.Abs(t - Mathf.Round(t));
            return Mathf.Clamp01(1f - f * 9f);
        }

        private static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 2147483647);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / (float)0xFFFFFF;
            }
        }

        private static float Hash(int n, int seed)
        {
            return Hash(n, n * 7 + 3, seed);
        }

        private static float ValueNoise(float x, float y, int seed)
        {
            int ix = Mathf.FloorToInt(x);
            int iy = Mathf.FloorToInt(y);
            float fx = x - ix;
            float fy = y - iy;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            float a = Hash(ix, iy, seed);
            float b = Hash(ix + 1, iy, seed);
            float c = Hash(ix, iy + 1, seed);
            float d = Hash(ix + 1, iy + 1, seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }
    }
}
