// PURPOSE: What a joker ICON actually looks like, measured once from the art, so "Parazit" can embed
// it by its real silhouette instead of by the rectangle it was cut from.
//
// The joker icons are imported non-readable and without mipmaps, so nothing on the CPU can ask them a
// question. But four things about embedding an icon in the membrane need to know the drawing, not its
// rect:
//
//   SILHOUETTE  the fibers hold on to the icon's EDGES - the left of the hand, the bottom of the
//               coin - and an edge taken from the sprite rect is a point in empty padding. The
//               masks in the shader (which side the film has eaten) have to follow the drawing too.
//   FIT         icons carry very different padding, so "a third of the cube" measured on the rect
//               gives a big Midas and a tiny coin. The visible bounds are what gets fitted.
//   COLOUR      the approach trail, the rim on a dark host and the contrast check all want the
//               icon's own average colour and luminance.
//   COMPLEXITY  a busy icon needs a little more room than a simple one to survive the downscale.
//
// So each icon is drawn ONCE, small, into a temporary render target and read back (a GPU blit and a
// ReadPixels - no import setting is changed and nothing is kept but the numbers and a small alpha
// grid). Cached per sprite for the rest of the run. If the device cannot do it (a null device, a
// headless run), the profile falls back to the rect itself - an ellipse for the edge and a neutral
// colour - and everything still works, only less precisely.
//
// n-space, used everywhere here and in ParasiteEmbeddedIcon.shader: -1..1 across the sprite RECT,
// y up. The visible bounds and every edge point are in it.

using System.Collections.Generic;
using UnityEngine;

namespace ProjectBlock.View
{
    /// <summary>The measured look of one icon sprite.</summary>
    public sealed class ParasiteIconProfile
    {
        /// <summary>The side of the square the icon is read into. Enough for a silhouette and an
        /// average; small enough to cost nothing.</summary>
        private const int Grid = 64;

        /// <summary>Alpha that counts as "the drawing" rather than soft edge or padding.</summary>
        private const float Solid = 0.35f;

        private static readonly Dictionary<Sprite, ParasiteIconProfile> cache =
            new Dictionary<Sprite, ParasiteIconProfile>();

        public Sprite Sprite { get; private set; }

        /// <summary>False when the read-back could not be done and everything below is the rect's
        /// fallback.</summary>
        public bool Measured { get; private set; }

        /// <summary>The sprite's rect in its texture, as the shader wants it: xMin, yMin, xMax, yMax
        /// in UV.</summary>
        public Vector4 UVRect { get; private set; }

        /// <summary>The silhouette's bounds inside the rect, in n.</summary>
        public Rect Visible { get; private set; }

        /// <summary>The silhouette's alpha-weighted centre, in n.</summary>
        public Vector2 Centroid { get; private set; }

        /// <summary>Its alpha-weighted average colour (gamma space, like every UI colour).</summary>
        public Color Average { get; private set; }

        public float Luminance { get; private set; }

        /// <summary>0 a plain round shape in one colour, 1 a busy outline in many.</summary>
        public float Complexity { get; private set; }

        private float[] alpha;

        public static ParasiteIconProfile For(Sprite sprite)
        {
            if (sprite == null)
            {
                return null;
            }
            ParasiteIconProfile p;
            if (!cache.TryGetValue(sprite, out p))
            {
                p = new ParasiteIconProfile(sprite);
                cache[sprite] = p;
            }
            return p;
        }

        private ParasiteIconProfile(Sprite sprite)
        {
            Sprite = sprite;
            Texture tex = sprite.texture;
            Rect r = sprite.textureRect;
            float tw = tex != null ? Mathf.Max(1, tex.width) : 1f;
            float th = tex != null ? Mathf.Max(1, tex.height) : 1f;
            UVRect = new Vector4(r.xMin / tw, r.yMin / th, r.xMax / tw, r.yMax / th);
            // The fallback: the whole rect is the drawing, in a neutral warm grey.
            Visible = new Rect(-0.9f, -0.9f, 1.8f, 1.8f);
            Centroid = Vector2.zero;
            Average = new Color(0.7f, 0.64f, 0.56f);
            Luminance = 0.65f;
            Complexity = 0.5f;
            try
            {
                Measure(tex, r, tw, th);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[block_bonk] Parazit: could not measure icon '" + sprite.name
                    + "' (" + e.Message + ") - embedding it by its rect");
            }
        }

        private void Measure(Texture tex, Rect r, float tw, float th)
        {
            if (tex == null || SystemInfo.graphicsDeviceType
                    == UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                return;
            }
            RenderTexture rt = RenderTexture.GetTemporary(Grid, Grid, 0, RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.sRGB);
            RenderTexture previous = RenderTexture.active;
            var read = new Texture2D(Grid, Grid, TextureFormat.RGBA32, false);
            try
            {
                // Clear first: Blit only writes what it samples, and a recycled temporary target
                // can still hold the last icon.
                RenderTexture.active = rt;
                GL.Clear(true, true, new Color(0f, 0f, 0f, 0f));
                Graphics.Blit(tex, rt, new Vector2(r.width / tw, r.height / th),
                    new Vector2(r.xMin / tw, r.yMin / th));
                RenderTexture.active = rt;
                read.ReadPixels(new Rect(0, 0, Grid, Grid), 0, 0);
                read.Apply(false);
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(rt);
            }
            Color32[] px = read.GetPixels32();
            Object.Destroy(read);
            Analyse(px);
        }

        private void Analyse(Color32[] px)
        {
            alpha = new float[Grid * Grid];
            float sumA = 0f;
            float sr = 0f, sg = 0f, sb = 0f;
            float cx = 0f, cy = 0f;
            int minX = Grid, minY = Grid, maxX = -1, maxY = -1;
            for (int y = 0; y < Grid; y++)
            {
                for (int x = 0; x < Grid; x++)
                {
                    Color32 c = px[y * Grid + x];
                    float a = c.a / 255f;
                    alpha[y * Grid + x] = a;
                    if (a <= 0.02f)
                    {
                        continue;
                    }
                    sumA += a;
                    // A plain Blit copies straight (not premultiplied) colour, so a soft edge pixel
                    // counts for as much as it covers.
                    sr += c.r / 255f * a;
                    sg += c.g / 255f * a;
                    sb += c.b / 255f * a;
                    cx += x * a;
                    cy += y * a;
                    if (a >= Solid)
                    {
                        minX = Mathf.Min(minX, x);
                        minY = Mathf.Min(minY, y);
                        maxX = Mathf.Max(maxX, x);
                        maxY = Mathf.Max(maxY, y);
                    }
                }
            }
            if (sumA < 1f || maxX < minX)
            {
                return; // nothing drawn - keep the fallback
            }
            Color avg = new Color(sr / sumA, sg / sumA, sb / sumA);
            Average = avg;
            Luminance = 0.299f * avg.r + 0.587f * avg.g + 0.114f * avg.b;
            Centroid = new Vector2(ToN(cx / sumA), ToN(cy / sumA));
            float x0 = ToN(minX - 0.5f);
            float y0 = ToN(minY - 0.5f);
            float x1 = ToN(maxX + 0.5f);
            float y1 = ToN(maxY + 0.5f);
            Visible = new Rect(x0, y0, x1 - x0, y1 - y0);

            // COMPLEXITY: how ragged the outline is against its area (a circle is 1), and how
            // much the luminance varies inside it. Both are what a downscale destroys first.
            int area = 0;
            int perimeter = 0;
            float lumSum = 0f;
            float lumSq = 0f;
            for (int y = 0; y < Grid; y++)
            {
                for (int x = 0; x < Grid; x++)
                {
                    bool inside = alpha[y * Grid + x] >= Solid;
                    if (!inside)
                    {
                        continue;
                    }
                    area++;
                    Color32 c = px[y * Grid + x];
                    float l = (0.299f * c.r + 0.587f * c.g + 0.114f * c.b) / 255f;
                    lumSum += l;
                    lumSq += l * l;
                    if (x == 0 || y == 0 || x == Grid - 1 || y == Grid - 1
                        || alpha[y * Grid + x - 1] < Solid || alpha[y * Grid + x + 1] < Solid
                        || alpha[(y - 1) * Grid + x] < Solid || alpha[(y + 1) * Grid + x] < Solid)
                    {
                        perimeter++;
                    }
                }
            }
            if (area > 0)
            {
                float roundness = perimeter * perimeter / (4f * Mathf.PI * area);
                float mean = lumSum / area;
                float spread = Mathf.Sqrt(Mathf.Max(0f, lumSq / area - mean * mean));
                Complexity = Mathf.Clamp01(0.5f * Mathf.Clamp01((roundness - 1.2f) / 2.5f)
                    + 0.5f * Mathf.Clamp01(spread / 0.28f));
            }
            Measured = true;
        }

        private static float ToN(float gridCoordinate)
        {
            return (gridCoordinate + 0.5f) / Grid * 2f - 1f;
        }

        private float AlphaAtN(Vector2 n)
        {
            int x = Mathf.RoundToInt((n.x + 1f) * 0.5f * Grid - 0.5f);
            int y = Mathf.RoundToInt((n.y + 1f) * 0.5f * Grid - 0.5f);
            if (alpha == null || x < 0 || y < 0 || x >= Grid || y >= Grid)
            {
                return 0f;
            }
            return alpha[y * Grid + x];
        }

        /// <summary>
        /// The point where a ray from the silhouette's centre, at <paramref name="angle"/> radians,
        /// leaves the drawing - in n. What a fiber grips. Walked OUTWARD and the last solid step
        /// kept, so a hole in the middle of an icon (a ring, a coin's inner edge) does not stop it
        /// short of the real outline. Without a measurement it is the visible bounds' ellipse.
        /// </summary>
        public Vector2 EdgePoint(float angle)
        {
            var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            Vector2 c = Centroid;
            if (alpha == null)
            {
                Vector2 half = Visible.size * 0.5f;
                return Visible.center + new Vector2(dir.x * half.x, dir.y * half.y) * 0.92f;
            }
            float step = 1f / Grid;
            Vector2 last = c;
            for (float t = 0f; t < 2.9f; t += step)
            {
                Vector2 p = c + dir * t;
                if (Mathf.Abs(p.x) > 1f || Mathf.Abs(p.y) > 1f)
                {
                    break;
                }
                if (AlphaAtN(p) >= Solid)
                {
                    last = p;
                }
            }
            return last;
        }
    }
}
