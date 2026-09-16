// PURPOSE: The small pieces "Elmas Kazma"'s break is drawn with, baked once - and the cutter that
// splits an obsidian cube's OWN face into the wedges it breaks into.
//
// EACH PIECE IS DEFINED BY WHAT IT MUST NOT BE (see quarry_mock*.png in the session scratchpad).
// The CRACK is a hairline with a cold white core and a desaturated blue edge, drawn as a few
// straight runs with small turns between them - angular and mineral, never a smooth curve and
// never a web. The STRIKE is a short TAPERED streak, bright at its head and thinning to nothing
// behind it: a lance of light with parallel sides is a laser, and a long flat one is a sword
// slash, and this is neither. The COLD RIM lights only the top and left edges of a cube, the way
// a cold light from above would catch polished stone; lit on all four sides it is a selection box.
// The DUST is a rhombus, because a round speck is glitter.
//
// THE SHARDS ARE THE CUBE. A cube does not break into generic chips recoloured cyan: it breaks
// along its own cracks. The view cuts the obsidian tile into wedges round the crack junction
// (a few lines through one point) and each wedge is a Sprite over the tile's own texture with
// its geometry overridden - so the pieces that fly are pieces of that very face, and together
// they are exactly the cube until they part. A tile that cannot be cut (packed tight) falls back
// to a dark procedural chip.

using System.Collections.Generic;
using UnityEngine;

namespace ProjectBlock.View
{
    public static class QuarryShapes
    {
        private const int Size = 64;

        public static readonly Color CrackCore = new Color(0.93f, 0.98f, 1f);
        public static readonly Color CrackEdge = new Color(0.47f, 0.67f, 0.84f);
        public static readonly Color Ice = new Color(0.67f, 0.88f, 0.96f);
        public static readonly Color Obsidian = new Color(0.13f, 0.10f, 0.17f);

        private static Sprite crack;
        private static Sprite strike;
        private static Sprite rim;
        private static Sprite dust;
        private static Sprite chip;
        private static Sprite plate;

        /// <summary>A hairline along +x, one unit long and one unit TALL (the core is the middle
        /// fifth), so a y scale of three core widths draws a core of one.</summary>
        public static Sprite Crack
        {
            get
            {
                if (crack == null)
                {
                    crack = Bake((u, v) =>
                    {
                        float d = Mathf.Abs(v) / 0.5f;
                        float core = Mathf.Clamp01((0.22f - d) / 0.08f);
                        float edge = Mathf.Pow(Mathf.Clamp01(1f - d), 1.6f) * 0.75f;
                        float ends = Mathf.Clamp01((0.5f - Mathf.Abs(u)) / 0.04f);
                        float a = Mathf.Max(core, edge) * ends;
                        Color c = Color.Lerp(CrackEdge, CrackCore, core);
                        c.a = a;
                        return c;
                    }, FilterMode.Bilinear);
                }
                return crack;
            }
        }

        /// <summary>The strike: its HEAD at +x, a bright core, a cyan edge, tapering to nothing
        /// behind.</summary>
        public static Sprite Strike
        {
            get
            {
                if (strike == null)
                {
                    strike = Bake((u, v) =>
                    {
                        float x = u + 0.5f; // 0 at the tail, 1 at the head
                        float half = x < 0.82f ? 0.42f * Mathf.Pow(x / 0.82f, 1.4f)
                            : 0.42f * Mathf.Sqrt(Mathf.Max(0f, 1f - (x - 0.82f) / 0.18f));
                        float d = Mathf.Abs(v);
                        float body = Mathf.Clamp01((half - d) / 0.06f);
                        float core = Mathf.Clamp01((half * 0.4f - d) / 0.04f) * Mathf.Clamp01(x * 1.4f - 0.3f);
                        Color c = Color.Lerp(Ice, Color.white, core);
                        c.a = Mathf.Max(body * 0.7f, core);
                        return c;
                    }, FilterMode.Bilinear);
                }
                return strike;
            }
        }

        /// <summary>A cold reflection along a cube's top and left edges only.</summary>
        public static Sprite Rim
        {
            get
            {
                if (rim == null)
                {
                    rim = Bake((u, v) =>
                    {
                        float top = Mathf.Clamp01(1f - (0.5f - v) / 0.12f) * Mathf.Clamp01((0.5f - Mathf.Abs(u)) / 0.2f + 0.3f);
                        float left = Mathf.Clamp01(1f - (u + 0.5f) / 0.12f) * Mathf.Clamp01((0.5f - Mathf.Abs(v)) / 0.2f + 0.3f);
                        float a = Mathf.Max(top, left);
                        return new Color(1f, 1f, 1f, a * a);
                    }, FilterMode.Bilinear);
                }
                return rim;
            }
        }

        /// <summary>A soft rounded white square the size of a cube body.</summary>
        public static Sprite Plate
        {
            get
            {
                if (plate == null)
                {
                    plate = Bake((u, v) =>
                    {
                        float qx = Mathf.Abs(u) - 0.40f;
                        float qy = Mathf.Abs(v) - 0.40f;
                        float d = Mathf.Sqrt(Mathf.Max(qx, 0f) * Mathf.Max(qx, 0f) + Mathf.Max(qy, 0f) * Mathf.Max(qy, 0f))
                            + Mathf.Min(Mathf.Max(qx, qy), 0f) - 0.08f;
                        return new Color(1f, 1f, 1f, Mathf.Clamp01(-d / 0.02f));
                    }, FilterMode.Bilinear);
                }
                return plate;
            }
        }

        public static Sprite Dust
        {
            get
            {
                if (dust == null)
                {
                    dust = Bake((u, v) =>
                    {
                        float d = Mathf.Abs(u) * 1.4f + Mathf.Abs(v);
                        return new Color(1f, 1f, 1f, Mathf.Clamp01((0.42f - d) / 0.08f));
                    }, FilterMode.Bilinear);
                }
                return dust;
            }
        }

        /// <summary>A small dark angular chip with a cold edge, baked in its own colours.</summary>
        public static Sprite Chip
        {
            get
            {
                if (chip == null)
                {
                    chip = Bake((u, v) =>
                    {
                        float d = Mathf.Min(Mathf.Min(0.36f - (u * 0.8f + v * 0.6f), 0.30f - (-u * 0.9f + v * 0.4f)),
                            Mathf.Min(0.26f + v, 0.40f - Mathf.Abs(u)));
                        float a = Mathf.Clamp01(d / 0.03f);
                        float edge = Mathf.Clamp01(1f - d / 0.07f);
                        Color c = Color.Lerp(Obsidian, Ice, edge * 0.45f);
                        c.a = a;
                        return c;
                    }, FilterMode.Bilinear);
                }
                return chip;
            }
        }

        // ---- the wedge cutter ------------------------------------------------------------------

        private static readonly Dictionary<long, Sprite> wedges = new Dictionary<long, Sprite>();

        /// <summary>
        /// A piece of <paramref name="tile"/>: the convex <paramref name="polygon"/> (unit cube
        /// space, centre 0, +y up) over the tile's own texture, pivoted on the cube centre so a
        /// renderer at the cube's position and scale draws it exactly where it was. Null when the
        /// tile cannot be cut. Cached by <paramref name="key"/>.
        /// </summary>
        public static Sprite Wedge(Sprite tile, IList<Vector2> polygon, long key)
        {
            if (tile == null || tile.texture == null || polygon == null || polygon.Count < 3)
            {
                return null;
            }
            key ^= (long)tile.GetInstanceID() << 32;
            Sprite cut;
            if (wedges.TryGetValue(key, out cut))
            {
                return cut;
            }
            if (wedges.Count > 256)
            {
                foreach (Sprite s in wedges.Values)
                {
                    if (s != null) { Object.Destroy(s); }
                }
                wedges.Clear();
            }
            cut = null;
            bool cuttable = !tile.packed || (tile.packingMode != SpritePackingMode.Tight
                && tile.packingRotation == SpritePackingRotation.None);
            if (cuttable)
            {
                Rect r = tile.packed ? tile.textureRect : tile.rect;
                float ppu = tile.pixelsPerUnit;
                try
                {
                    // Pivot in normalised rect space = the tile's own pivot, so the cube centre
                    // stays the cube centre.
                    var pivot = new Vector2(tile.pivot.x / r.width, tile.pivot.y / r.height);
                    cut = Sprite.Create(tile.texture, r, pivot, ppu, 0, SpriteMeshType.FullRect);
                    int count = polygon.Count;
                    var vertices = new Vector2[count];
                    for (int i = 0; i < count; i++)
                    {
                        Vector2 px = tile.pivot + polygon[i] * ppu;
                        vertices[i] = new Vector2(Mathf.Clamp(px.x, 0f, r.width), Mathf.Clamp(px.y, 0f, r.height));
                    }
                    var triangles = new ushort[(count - 2) * 3];
                    for (int t = 0; t < count - 2; t++)
                    {
                        triangles[t * 3] = 0;
                        triangles[t * 3 + 1] = (ushort)(t + 1);
                        triangles[t * 3 + 2] = (ushort)(t + 2);
                    }
                    cut.OverrideGeometry(vertices, triangles);
                    cut.name = "ObsidianWedge";
                }
                catch (System.Exception)
                {
                    cut = null;
                }
            }
            wedges[key] = cut;
            return cut;
        }

        /// <summary>
        /// The square split into wedges by lines through <paramref name="junction"/>: two per
        /// line, in angle order. Each wedge is the square clipped by two half-planes, so it is
        /// convex and a fan triangulates it.
        /// </summary>
        public static List<List<Vector2>> SplitSquare(Vector2 junction, IList<float> lineAngles)
        {
            var rays = new List<float>();
            for (int i = 0; i < lineAngles.Count; i++)
            {
                rays.Add(Mathf.Repeat(lineAngles[i], Mathf.PI * 2f));
                rays.Add(Mathf.Repeat(lineAngles[i] + Mathf.PI, Mathf.PI * 2f));
            }
            rays.Sort();
            var result = new List<List<Vector2>>();
            var square = new List<Vector2>
            {
                new Vector2(-0.5f, -0.5f), new Vector2(0.5f, -0.5f), new Vector2(0.5f, 0.5f), new Vector2(-0.5f, 0.5f)
            };
            for (int i = 0; i < rays.Count; i++)
            {
                float a1 = rays[i];
                float a2 = rays[(i + 1) % rays.Count];
                // Left of ray a1, right of ray a2.
                var n1 = new Vector2(-Mathf.Sin(a1), Mathf.Cos(a1));
                var n2 = new Vector2(Mathf.Sin(a2), -Mathf.Cos(a2));
                List<Vector2> p = Clip(square, n1, Vector2.Dot(n1, junction));
                p = Clip(p, n2, Vector2.Dot(n2, junction));
                if (p.Count >= 3)
                {
                    result.Add(p);
                }
            }
            return result;
        }

        private static List<Vector2> Clip(List<Vector2> poly, Vector2 n, float c)
        {
            var output = new List<Vector2>();
            for (int i = 0; i < poly.Count; i++)
            {
                Vector2 a = poly[i];
                Vector2 b = poly[(i + 1) % poly.Count];
                float da = Vector2.Dot(n, a) - c;
                float db = Vector2.Dot(n, b) - c;
                if (da >= 0f)
                {
                    output.Add(a);
                }
                if ((da >= 0f) != (db >= 0f))
                {
                    output.Add(Vector2.Lerp(a, b, da / (da - db)));
                }
            }
            return output;
        }

        public static float Area(IList<Vector2> p)
        {
            float a = 0f;
            for (int i = 0; i < p.Count; i++)
            {
                Vector2 q = p[i];
                Vector2 r = p[(i + 1) % p.Count];
                a += q.x * r.y - r.x * q.y;
            }
            return Mathf.Abs(a) * 0.5f;
        }

        public static Vector2 Centroid(IList<Vector2> p)
        {
            Vector2 sum = Vector2.zero;
            for (int i = 0; i < p.Count; i++)
            {
                sum += p[i];
            }
            return sum / Mathf.Max(1, p.Count);
        }

        // ---- baking ----------------------------------------------------------------------------

        private delegate Color Field(float u, float v);

        private static Sprite Bake(Field field, FilterMode filter)
        {
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = filter
            };
            var px = new Color[Size * Size];
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float u = (x + 0.5f) / Size - 0.5f;
                    float v = (y + 0.5f) / Size - 0.5f;
                    px[y * Size + x] = field(u, v);
                }
            }
            tex.SetPixels(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), Size);
        }
    }
}
