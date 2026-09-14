// PURPOSE: "Tılsım"'s baked silhouettes - the RUNE KNOT on the board's edge, the TALISMAN SEED a
// harvested ghost leaves behind, the KERNEL at the heart of a ghost being taken, the CORNER RUNE
// that marks a cell as bonus ground, and the FLAKE it comes apart into. Rasterised once into small
// alpha textures and shared, as the parasite's and Mapus's are.
//
// THE VINE ITSELF IS NOT HERE ANY MORE. It was drawn - a root hierarchy of tapered tube segments
// with forks, tips and contact shadows - and it is now a nine-frame sprite sheet the artist drew,
// at Resources/Art/Fx/talisman_vine_sheet.png. What is left in this file is the small stuff around
// it, which is still cheaper to bake than to author.
//
// THE PRIMITIVE HERE IS THE FUSED CIRCLE AGAIN, and that is a deliberate choice against the last
// system: Mapus is forged iron and needed polygons with flat runs between crisp corners. This is
// the opposite pole - a growing, spiritual, half-organic thing - and it needs contours with no
// flat anywhere on them. Using the wrong one is what made the parasite read as a flower and Mapus
// read as a rotor, and the two mistakes were the same mistake.
//
// The one rule the shapes all obey: EVERY SHAPE FILLS ITS BOX. A tuning number in the view that
// says "a vine 4% of a cell wide" has to produce one 4% of a cell wide, and a shape drawn inside
// half its box quietly halves every number that refers to it.

using UnityEngine;

namespace ProjectBlock.View
{
    /// <summary>Tılsım's baked silhouettes. All static, all shared, all made on first use.</summary>
    public static class TalismanShapes
    {
        private const int Pixels = 96;

        private static Sprite knot;

        private static Sprite seed;

        private static Sprite kernel;

        private static Sprite recess;

        private static Sprite[] corners;

        private static Sprite flake;

        private static Sprite frame;

        /// <summary>
        /// A RUNE KNOT: the small gold binding at a vine's joint. An open, slightly squashed ring
        /// with a short tie across it - never a closed circle, because a closed ring in the middle
        /// of anything reads as a socket or a hole.
        /// </summary>
        public static Sprite Knot
        {
            get
            {
                if (knot == null)
                {
                    // A LASHING, not a ring. An open ring with a tie across it is the letter e,
                    // exactly - the same glyph trap Mapus's brand fell into with a G. A cord bound
                    // round a stem is three short bands and two nubs, and no alphabet has that.
                    knot = Bake("TalismanKnot", delegate (float x, float y)
                    {
                        // THIN bands with real gaps between them. At radius 0.2 the three merged
                        // into one slab, which is a block and not a binding: a lashing is read
                        // from the gaps as much as from the cord.
                        float d = Bar(x, y, -0.72f, 0.5f, 0.72f, 0.36f, 0.12f);
                        d = Mathf.Max(d, Bar(x, y, -0.78f, 0.04f, 0.78f, -0.08f, 0.115f));
                        d = Mathf.Max(d, Bar(x, y, -0.64f, -0.44f, 0.6f, -0.56f, 0.1f));
                        // The two ends of the cord, tucked under and hanging.
                        d = Mathf.Max(d, Bar(x, y, 0.6f, -0.5f, 0.82f, -0.86f, 0.09f));
                        return d;
                    });
                }
                return knot;
            }
        }

        /// <summary>
        /// THE TALISMAN SEED a reclaimable ghost leaves behind: a small droplet-flame, wider at its
        /// shoulders and drawn to a point at the top. Not a dot - a dot is a status light, and this
        /// is the one thing on the board that says "ground was claimed here".
        /// </summary>
        public static Sprite Seed
        {
            get
            {
                if (seed == null)
                {
                    seed = Bake("TalismanSeed", delegate (float x, float y)
                    {
                        float d = Blob(x, y, 0f, -0.24f, 0.66f);
                        d = Fuse(d, Blob(x, y, 0f, 0.24f, 0.42f), 0.34f);
                        d = Fuse(d, Blob(x, y, 0.02f, 0.72f, 0.18f), 0.26f);
                        return d;
                    });
                }
                return seed;
            }
        }

        /// <summary>The soul kernel inside a ghost as it comes apart: a soft irregular core, a
        /// little wider than it is tall. It becomes a seed where the ground can be claimed and
        /// scatters where it cannot.</summary>
        public static Sprite Kernel
        {
            get
            {
                if (kernel == null)
                {
                    kernel = Bake("TalismanKernel", delegate (float x, float y)
                    {
                        float d = Blob(x, y, -0.08f, 0f, 0.72f);
                        d = Fuse(d, Blob(x, y, 0.3f, 0.14f, 0.48f), 0.34f);
                        d = Fuse(d, Blob(x, y, 0.06f, -0.34f, 0.4f), 0.3f);
                        return d;
                    });
                }
                return kernel;
            }
        }

        /// <summary>
        /// A CORNER RUNE: the mark that says a cell is BONUS GROUND. Four of them sit at a cell's
        /// corners and they never join up.
        ///
        /// That gap is the entire idea. The board's ordinary cells have a closed structural frame,
        /// and a line runs through those frames; bonus ground is play area that no line waits for.
        /// So its frame is OPEN - four short curved strokes with nothing between them - and the
        /// rule is legible without a word of UI. Four variants so a cell's corners are a set rather
        /// than one stroke rotated four times.
        /// </summary>
        /// <summary>
        /// THE RECESS the cover comes out of: the small dark split in the board's own rim.
        ///
        /// IT USED TO BE THE THICKET - one of these on every reclaimed cell, and the dark mass
        /// under the claim was the union of them. That whole idea is gone: a field made of
        /// cell-shaped lumps announces the grid it was made from, and what lies under a claim is
        /// now baked from the cells themselves in TalismanStain, where a bridge and a corner
        /// merge can close the seams a pile of blobs never could.
        ///
        /// What kept it alive is that it is still the right shape for a HOLE: a ragged near-black
        /// mass with nothing straight on it, which is what a split in a surface looks like from
        /// above. Crossed bars for the middle, four unequal corner lumps, four more off the
        /// sides, all generously fused, and a soft edge - because a hard-edged dark shape is an
        /// object lying there rather than an opening.
        /// </summary>
        public static Sprite Recess
        {
            get
            {
                if (recess == null)
                {
                    recess = Bake("TalismanRecess", delegate (float x, float y)
                    {
                        // Everything is at 0.95 so the soft edge below still fits in the box -
                        // a gradient that runs off the texture is cut off square, which is the
                        // one silhouette this shape must never have.
                        const float k = 0.95f;
                        float d = Bar(x, y, -0.399f, 0.019f, 0.399f, -0.019f, 0.475f);
                        d = Fuse(d, Bar(x, y, 0.019f, -0.399f, -0.019f, 0.399f, 0.456f), 0.209f);
                        d = Fuse(d, Blob(x, y, -0.418f, 0.418f, 0.513f), 0.209f);
                        d = Fuse(d, Blob(x, y, 0.437f, 0.399f, 0.475f), 0.209f);
                        d = Fuse(d, Blob(x, y, 0.399f, -0.437f, 0.504f), 0.209f);
                        d = Fuse(d, Blob(x, y, -0.437f, -0.399f, 0.466f), 0.209f);
                        d = Fuse(d, Blob(x, y, -0.095f, 0.589f, 0.285f), 0.171f);
                        d = Fuse(d, Blob(x, y, 0.627f, 0.048f, 0.247f), 0.171f);
                        d = Fuse(d, Blob(x, y, 0.048f, -0.627f, 0.266f), 0.171f);
                        d = Fuse(d, Blob(x, y, -0.608f, -0.076f, 0.257f), 0.171f);
                        return Soft(d, 0.05f * k);
                    });
                }
                return recess;
            }
        }

        // THE VEIL AND THE PERIMETER LOBE USED TO BE BAKED HERE and both are gone, for two
        // different reasons worth keeping straight.
        //
        // The veil was a lens-shaped sprite stretched between two branches. It is now a small
        // MESH (TalismanTendril.Pocket) because a sprite can only be a convex blob between two
        // points, and what a skin over a hole actually does is hang CONCAVE between three of
        // them - a bulge reads as a bubble sitting in the gap rather than as the gap being
        // covered.
        //
        // The lobe was a tongue of darkness over the claim's rim, drawn as its own sprite over
        // the membrane. It is now part of the stain's own field (TalismanStain's edge bleed):
        // a tongue laid OVER a dark field is a second dark shape with its own soft edge, and the
        // two edges crossing is exactly the bead-on-the-rim look that made the claim read as a
        // panel with decorations round it. Baked into the field it is the same mass.

        public static Sprite Corner(int variant)
        {
            if (corners == null)
            {
                corners = new Sprite[4];
            }
            int i = ((variant % corners.Length) + corners.Length) % corners.Length;
            if (corners[i] == null)
            {
                float bow = 0.24f + i * 0.05f;
                float thin = 0.115f - i * 0.008f;
                corners[i] = Bake("TalismanCorner" + i, delegate (float x, float y)
                {
                    // THE STROKE FILLS ITS OWN BOX and the view places four small ones at a cell's
                    // corners - the first pass drew a cell-sized sprite with the mark in one
                    // corner of it, which made every corner enormous and nearly closed the frame
                    // it was supposed to leave open. Two thin strokes off a shared elbow, each
                    // bowed away from it: a talisman stroke, not the hard L of a UI bracket.
                    float d = Bar(x, y, -0.86f, 0.86f, 0.72f, 0.86f - bow, thin);
                    d = Fuse(d, Bar(x, y, -0.86f, 0.86f, -0.86f + bow, -0.72f, thin), 0.06f);
                    d = Fuse(d, Blob(x, y, -0.82f, 0.82f, thin * 1.5f), 0.07f);
                    return d;
                }, 0.95f);
            }
            return corners[i];
        }

        /// <summary>
        /// DEV ONLY: a thin open square, for the lab's "which cells were actually reclaimed?"
        /// overlay and for the bounding rectangle drawn next to it.
        ///
        /// It exists so that the one question this whole system is judged on - is the darkness
        /// following the CELLS or the BOX? - can be looked at instead of argued about.
        /// </summary>
        public static Sprite CellFrame
        {
            get
            {
                if (frame == null)
                {
                    frame = Bake("TalismanCellFrame", delegate (float x, float y)
                    {
                        const float k = 0.94f;
                        const float t = 0.035f;
                        float outer = Mathf.Min(k - Mathf.Abs(x), k - Mathf.Abs(y));
                        float inner = Mathf.Min(k - t - Mathf.Abs(x), k - t - Mathf.Abs(y));
                        return Mathf.Min(outer, -inner);
                    });
                }
                return frame;
            }
        }

        /// <summary>A flake of the ground coming apart when the gift is recalled: a thin rounded
        /// shard, never a square chunk. Bonus ground does not shatter like stone - it stops being
        /// matter.</summary>
        public static Sprite Flake
        {
            get
            {
                if (flake == null)
                {
                    flake = Bake("TalismanFlake", delegate (float x, float y)
                    {
                        float d = Blob(x, y, -0.2f, -0.1f, 0.56f);
                        d = Fuse(d, Blob(x, y, 0.34f, 0.16f, 0.4f), 0.3f);
                        d = Mathf.Min(d, -Blob(x, y, 0.3f, -0.72f, 0.52f) + 0.02f);
                        return d;
                    }, 0.9f);
                }
                return flake;
            }
        }

        // =================================================================== the primitives

        private delegate float Field(float x, float y);

        private static float Blob(float x, float y, float cx, float cy, float r)
        {
            float dx = x - cx;
            float dy = y - cy;
            return r - Mathf.Sqrt(dx * dx + dy * dy);
        }

        private static float Bar(float x, float y, float ax, float ay, float bx, float by, float r)
        {
            float dx = bx - ax;
            float dy = by - ay;
            float len = dx * dx + dy * dy;
            float t = len < 1e-6f ? 0f : Mathf.Clamp01(((x - ax) * dx + (y - ay) * dy) / len);
            float px = x - (ax + dx * t);
            float py = y - (ay + dy * t);
            return r - Mathf.Sqrt(px * px + py * py);
        }

        /// <summary>A SMOOTH union - the shapes swell into one another instead of meeting at a
        /// crease, which is what makes several circles read as one grown thing.</summary>
        /// <summary>
        /// WIDENS A SHAPE'S EDGE from the one-texel default into a real gradient.
        ///
        /// The baker turns the distance field into alpha over a single texel, which is right for
        /// a mark with an outline. It is wrong for a mass whose job is to be DEPTH: a hard-edged
        /// dark patch reads as an object lying there - a dark tile instead of a pale one - where
        /// the same patch with a soft edge reads as shadow under what is standing on it.
        ///
        /// The ramp costs coverage: the fully opaque part of the shape shrinks by
        /// <paramref name="width"/>, so whatever has to be buried needs the shape scaled up to
        /// match.
        /// </summary>
        private static float Soft(float d, float width)
        {
            return d * (2f / Pixels) / Mathf.Max(width, 1e-4f);
        }

        private static float Fuse(float a, float b, float k)
        {
            float h = Mathf.Clamp01(0.5f + 0.5f * (a - b) / Mathf.Max(k, 1e-4f));
            return Mathf.Lerp(b, a, h) + k * h * (1f - h);
        }

        // =================================================================== the baker

        private static Sprite Bake(string name, Field field)
        {
            return Bake(name, field, 1f);
        }

        private static Sprite Bake(string name, Field field, float maxAlpha)
        {
            var tex = new Texture2D(Pixels, Pixels, TextureFormat.RGBA32, false)
            {
                name = name,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            var pixels = new Color32[Pixels * Pixels];
            float step = 2f / Pixels;
            for (int j = 0; j < Pixels; j++)
            {
                float y = -1f + (j + 0.5f) * step;
                for (int i = 0; i < Pixels; i++)
                {
                    float x = -1f + (i + 0.5f) * step;
                    float a = Mathf.Clamp01(field(x, y) / step * 0.5f + 0.5f) * maxAlpha;
                    pixels[j * Pixels + i] =
                        new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, false);
            Sprite made = Sprite.Create(tex, new Rect(0f, 0f, Pixels, Pixels),
                new Vector2(0.5f, 0.5f), Pixels);
            made.hideFlags = HideFlags.HideAndDontSave;
            return made;
        }
    }
}
