// PURPOSE: The SILHOUETTES "Parazit" is made of, baked once into small textures.
//
// This file exists because of a specific failure. The first parasite was drawn entirely by scaling
// and rotating ViewUtil.RoundedSprite, and a rounded rectangle is the only thing that can ever come
// out of that: four rounded squares in the corners, four straight-ish strips to the middle and one
// small square in it. That reads as a UI lock or a sci-fi bracket bolted onto the cube - geometry,
// not an organism. No amount of tuning fixes it, because the SHAPES were wrong.
//
// So the shapes are generated: a lobed asymmetric heart, root clusters of two or three fused toes
// spreading over a corner, a seed for the passenger, and a soft irregular membrane patch. Each is a
// signed-distance field of overlapping circles rasterised to alpha, which is exactly how a stylised
// organic blob is built - round parts fused into one silhouette, never a boolean of rectangles.
//
// Everything is DETERMINISTIC from a seed, so the same host looks the same every time and the lab
// can be compared frame for frame. Everything is baked ONCE and shared: a board full of hosts costs
// four textures, not four per host. Tendrils are not here - they are drawn as segments along a
// curve by the view, because a curve that can be tensioned and broken in the middle cannot be a
// baked strip.

using UnityEngine;

namespace ProjectBlock.View
{
    /// <summary>The parasite's baked silhouettes. All static, all shared, all made on first use.
    /// </summary>
    public static class ParasiteShapes
    {
        private const int Pixels = 96;

        private static Sprite heart;

        private static Sprite[] roots;

        private static Sprite essence;

        private static Sprite membrane;

        private static Sprite tendril;

        /// <summary>
        /// THE HOST HEART: a small organic body, not a circle and not a square. Three overlapping
        /// lobes with the upper one fullest and the lower ones drawn in, plus a slight lean, so the
        /// contour is irregular but controlled - a seed or a bud rather than a gem. Its lobes are
        /// what a perfect circle cannot give: a silhouette with a top and a bottom.
        /// </summary>
        public static Sprite Heart
        {
            get
            {
                if (heart == null)
                {
                    heart = Bake("ParasiteHeart", delegate (float x, float y)
                    {
                        // Three fused lobes. The upper one is the biggest, so the body reads as
                        // growing UP out of the cube's surface.
                        float d = Blob(x, y, -0.02f, 0.16f, 0.52f);
                        d = Fuse(d, Blob(x, y, -0.30f, -0.14f, 0.40f), 0.22f);
                        d = Fuse(d, Blob(x, y, 0.28f, -0.18f, 0.36f), 0.22f);
                        // And one small asymmetric shoulder, so it is never mirror-symmetric.
                        d = Fuse(d, Blob(x, y, 0.26f, 0.30f, 0.24f), 0.18f);
                        return d;
                    });
                }
                return heart;
            }
        }

        /// <summary>
        /// A ROOT CLUSTER: two or three fused toes spreading away from the middle, wider where they
        /// meet the surface and rounded at their tips. Three variants so the four corners are the
        /// same family without being four copies - the asymmetry is what stops them reading as four
        /// identical squares.
        /// </summary>
        public static Sprite Root(int variant)
        {
            if (roots == null)
            {
                roots = new Sprite[3];
            }
            int i = ((variant % roots.Length) + roots.Length) % roots.Length;
            if (roots[i] == null)
            {
                roots[i] = BakeRoot(i);
            }
            return roots[i];
        }

        private static Sprite BakeRoot(int variant)
        {
            // The cluster grows from the top of the texture (where the tendril arrives) down and
            // outward into toes.
            float spread = 0.26f + variant * 0.03f;
            float lean = (variant - 1) * 0.10f;
            int toes = variant == 1 ? 2 : 3;
            // THE FUSE IS THE WHOLE THING. Too generous and the toes melt into the pad and the
            // cluster reads as one torso; too mean and they come apart into separate islands. It
            // has to leave a visible WAIST between each toe while they stay one body.
            const float ToeFuse = 0.085f;
            const float TipFuse = 0.065f;
            return Bake("ParasiteRoot" + variant, delegate (float x, float y)
            {
                // The pad the tendril lands on.
                float d = Blob(x, y, lean * 0.4f, 0.34f, 0.36f);
                // Toes fanning down and out, each a little different, each still attached.
                for (int t = 0; t < toes; t++)
                {
                    float f = toes == 1 ? 0f : (t / (float)(toes - 1)) * 2f - 1f;
                    float tx = lean + f * spread;
                    float ty = -0.06f - Mathf.Abs(f) * 0.10f + (t % 2 == 0 ? 0.03f : -0.04f);
                    float r = 0.24f - Mathf.Abs(f) * 0.03f + (t == 1 ? 0.02f : 0f);
                    d = Fuse(d, Blob(x, y, tx, ty, r), ToeFuse);
                    // A rounded tip just past each toe, so it tapers instead of ending flat.
                    d = Fuse(d, Blob(x, y, tx * 1.22f, ty - 0.22f, r * 0.52f), TipFuse);
                }
                return d;
            });
        }

        private static Sprite[] patches;

        /// <summary>
        /// A NECROTIC PATCH: a thicker, DEAD region of the membrane. These are what stop the wrap
        /// reading as clean glass - opaque, matte, almost unlit, with a dried curled contour. Four
        /// variants, chosen two to four at a time per host so no two are laid out alike.
        /// </summary>
        public static Sprite Patch(int variant)
        {
            if (patches == null)
            {
                patches = new Sprite[4];
            }
            int i = ((variant % patches.Length) + patches.Length) % patches.Length;
            if (patches[i] == null)
            {
                patches[i] = BakePatch(i);
            }
            return patches[i];
        }

        private static Sprite BakePatch(int variant)
        {
            float turn = variant * 1.31f;
            float squash = 0.8f + variant * 0.08f;
            return Bake("ParasitePatch" + variant, delegate (float x, float y)
            {
                float cx = Mathf.Cos(turn);
                float sy = Mathf.Sin(turn);
                float rx = x * cx - y * sy;
                float ry = (x * sy + y * cx) / squash;
                // A dried lobe: one main body with two smaller ones budding off it, fused loosely
                // enough that the contour keeps its dents - a dead petal, not a disc.
                float d = Blob(rx, ry, -0.06f, 0.04f, 0.46f);
                d = Fuse(d, Blob(rx, ry, 0.30f, -0.16f, 0.30f), 0.11f);
                d = Fuse(d, Blob(rx, ry, -0.28f, -0.24f, 0.24f), 0.09f);
                d = Fuse(d, Blob(rx, ry, 0.12f, 0.34f, 0.22f), 0.08f);
                // A bite taken out of one side, so it reads as having dried and curled rather than
                // having been drawn.
                float bite = Blob(rx, ry, 0.34f + variant * 0.04f, 0.30f, 0.22f);
                d = Mathf.Min(d, -bite + 0.02f);
                return d;
            });
        }

        /// <summary>
        /// THE PASSENGER'S ESSENCE: a small seed, wider at its shoulders and drawn to a soft point
        /// at the bottom. Not a dot - a dot in the middle of a shape is a status light, and this has
        /// to read as something alive being carried.
        /// </summary>
        public static Sprite Essence
        {
            get
            {
                if (essence == null)
                {
                    essence = Bake("ParasiteEssence", delegate (float x, float y)
                    {
                        float d = Blob(x, y, 0f, 0.12f, 0.46f);
                        d = Fuse(d, Blob(x, y, 0f, -0.16f, 0.34f), 0.20f);
                        d = Fuse(d, Blob(x, y, 0f, -0.42f, 0.16f), 0.14f);
                        return d;
                    });
                }
                return essence;
            }
        }

        /// <summary>
        /// THE MEMBRANE: a soft irregular patch that sits under the heart and the tendrils' inner
        /// ends. Very low contrast - it is there to say the parasite has SETTLED onto the surface
        /// rather than been printed on it, and it must never read as a puddle.
        /// </summary>
        public static Sprite Membrane
        {
            get
            {
                if (membrane == null)
                {
                    membrane = Bake("ParasiteMembrane", delegate (float x, float y)
                    {
                        float d = Blob(x, y, 0f, 0f, 0.62f);
                        d = Fuse(d, Blob(x, y, -0.34f, 0.22f, 0.34f), 0.34f);
                        d = Fuse(d, Blob(x, y, 0.30f, -0.26f, 0.38f), 0.34f);
                        d = Fuse(d, Blob(x, y, 0.24f, 0.32f, 0.26f), 0.30f);
                        return d;
                    }, 0.55f);
                }
                return membrane;
            }
        }

        /// <summary>
        /// ONE SEGMENT OF A TENDRIL: a soft lozenge, longer than it is wide. A tendril is a chain of
        /// these laid along a curve at varying widths, which is how it gets a real bend and a real
        /// taper - and how it can be broken in the middle, with each side retracting to its own end.
        /// </summary>
        public static Sprite TendrilSegment
        {
            get
            {
                if (tendril == null)
                {
                    tendril = Bake("ParasiteTendril", delegate (float x, float y)
                    {
                        // A capsule: two circles fused along the vertical axis.
                        float d = Blob(x, y, 0f, 0.22f, 0.42f);
                        d = Fuse(d, Blob(x, y, 0f, -0.22f, 0.42f), 0.30f);
                        return d;
                    });
                }
                return tendril;
            }
        }

        // =================================================================== the baker

        private delegate float Field(float x, float y);

        /// <summary>Signed distance to a circle, positive inside - the one primitive everything
        /// here is built from.</summary>
        private static float Blob(float x, float y, float cx, float cy, float r)
        {
            float dx = x - cx;
            float dy = y - cy;
            return r - Mathf.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>A SMOOTH union: the two shapes swell into each other instead of meeting at a
        /// crease. This is what makes several circles read as one organic body rather than as a
        /// snowman.</summary>
        private static float Fuse(float a, float b, float k)
        {
            float h = Mathf.Clamp01(0.5f + 0.5f * (a - b) / Mathf.Max(k, 1e-4f));
            return Mathf.Lerp(b, a, h) + k * h * (1f - h);
        }

        private static Sprite Bake(string name, Field field)
        {
            return Bake(name, field, 1f);
        }

        /// <summary>Rasterises a field into an alpha texture: one pixel of ramp across the contour,
        /// so the silhouette is smooth without being a soft blur.</summary>
        private static Sprite Bake(string name, Field field, float maxAlpha)
        {
            var tex = new Texture2D(Pixels, Pixels, TextureFormat.RGBA32, false);
            tex.name = name;
            var pixels = new Color32[Pixels * Pixels];
            // The ramp is one pixel wide in field units.
            float ramp = 2f / Pixels;
            for (int y = 0; y < Pixels; y++)
            {
                for (int x = 0; x < Pixels; x++)
                {
                    // -1..1 across the texture.
                    float fx = (x + 0.5f) / Pixels * 2f - 1f;
                    float fy = (y + 0.5f) / Pixels * 2f - 1f;
                    float d = field(fx, fy);
                    float a = Mathf.Clamp01(d / ramp + 0.5f) * maxAlpha;
                    pixels[y * Pixels + x] = new Color(1f, 1f, 1f, a);
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.hideFlags = HideFlags.HideAndDontSave;
            return Sprite.Create(tex, new Rect(0, 0, Pixels, Pixels), new Vector2(0.5f, 0.5f),
                Pixels, 0, SpriteMeshType.FullRect);
        }
    }
}
