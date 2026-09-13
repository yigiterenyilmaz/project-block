// PURPOSE: "Mapus"'s baked silhouettes - the WARDEN RIB, its EDGE SOCKET, the WARDEN SEAL and the
// brand pressed into it. Signed-distance fields rasterised once into small alpha textures and
// shared by every seal on the board, as ParasiteShapes does for the parasite.
//
// THEY ARE BAKED BECAUSE WHAT THIS BOSS IS, IS A SHAPE. Drawn out of the generated rounded
// rectangle the rest of the debug view uses, a seal can only ever be a bar, a box or a dot - four
// bars round a cell is prison-bars clipart and a dot between them is a status light.
//
// AND THEY ARE POLYGONS, NOT FUSED CIRCLES. The parasite is built from circles smooth-unioned
// together because an organism has no flat anywhere on it. Mapus is the opposite: FORGED IRON, and
// the first pass proved what the wrong primitive costs - fused circles gave four soft petals round
// a red middle, which reads as a FLOWER. Iron needs flat runs, hard shoulders, a real taper and a
// crisp corner with only a hint of a fillet on it, and that is a convex polygon with a small round
// applied - so that is what these are, unioned sharply rather than smoothly.
//
// ONE MORE THING CARRIES THE WHOLE READ: the four hooks all turn the SAME WAY round the middle.
// Four symmetric tips pointing inward are a compass rose; four tips all leaning clockwise are an
// iris closing, and closing is the boss's entire verb.

using UnityEngine;

namespace ProjectBlock.View
{
    /// <summary>Mapus's baked silhouettes. All static, all shared, all made on first use.</summary>
    public static class MapusShapes
    {
        private const int Pixels = 96;

        private static Sprite[] ribs;

        private static Sprite socket;

        private static Sprite seal;

        private static Sprite brand;

        private static Sprite notch;

        /// <summary>
        /// A WARDEN RIB: the heavy iron that closes on the cell. Drawn pointing UP the sprite with
        /// its root at the bottom, so the view turns one copy into each of the four sides and the
        /// root always sits in its socket.
        ///
        /// Its job is not to be a bar. A broad square-shouldered root, a shaft that TAPERS as it
        /// reaches in, and a tip that HOOKS to one side - and the hook is the same way round on all
        /// four, so the set reads as an iris closing rather than as four things pointing at a
        /// middle. Three variants: same ironwork, not four copies of one stamp.
        /// </summary>
        public static Sprite Rib(int variant)
        {
            if (ribs == null)
            {
                ribs = new Sprite[3];
            }
            int i = ((variant % ribs.Length) + ribs.Length) % ribs.Length;
            if (ribs[i] == null)
            {
                ribs[i] = BakeRib(i);
            }
            return ribs[i];
        }

        private static Sprite BakeRib(int variant)
        {
            // EVERY SHAPE IN THIS FILE FILLS ITS BOX. x from -1 to 1 is the rib's own WIDTH and
            // y from -1 (the root, in the socket) to 1 (the end of the hook) is its LENGTH, so
            // when the view asks for a rib 18% of a cell wide it gets one 18% of a cell wide.
            //
            // WIDE AT THE ROOT, NARROW AT THE TIP, ALL THE WAY DOWN. This is the single line that
            // decides whether the four of them read as closing IN from the board's edges or as a
            // fan opening OUT of the middle, and the first pass got it wrong in a way worth
            // writing down: the shaft tapered correctly, but the HOOK at its tip was nearly as
            // wide as the root and stuck out sideways - so the silhouette was narrow in the middle
            // and wide at BOTH ends, offset to one side. That is a fan blade by definition, and
            // four of them leaning the same way is a shuriken. It is not enough for the shaft to
            // taper; the WHOLE piece has to.
            // A BOLT, NOT A BLADE - and the difference is that a bolt barely tapers at all.
            //
            // Two passes were lost to this. The first put a wide hook at the tip, so the piece was
            // wide at both ends: a fan blade. The second tapered hard from a wide root to a point,
            // which is ALSO a blade - four of those converging on a middle is a rotor however
            // chunky they are. The mass a prison bolt has is not in the bolt: it is in the HOUSING
            // it comes out of, which is a separate piece sunk into the cell's wall. So the shaft
            // here is nearly parallel-sided, its root only a little wider than its shank, and it
            // ends in a BLOCKY latch head that is NARROWER than the shaft rather than flared.
            float shank = 0.74f - variant * 0.02f;
            float neck = 0.62f - variant * 0.02f;
            float head = 0.66f - variant * 0.03f;
            float reach = 0.90f + variant * 0.03f;
            float[] shaft =
            {
                -0.96f, -1.00f,
                0.96f, -1.00f,
                shank, -0.62f,
                shank - 0.04f, 0.34f,
                neck, 0.56f,
                head, 0.70f,
                head - 0.02f, reach,
                -head - 0.03f, reach,
                -neck - 0.04f, 0.56f,
                -shank - 0.02f, 0.34f,
                -shank - 0.02f, -0.62f
            };
            // THE LATCH LIP: a tiny inward step on the very end, a couple of pixels of it. Just
            // enough that the head reads as catching rather than stopping; never wider than the
            // shank, and never a claw.
            float lipOut = head + 0.22f + variant * 0.02f;
            float[] hook =
            {
                -head * 0.5f, reach - 0.18f,
                head, reach - 0.16f,
                lipOut, reach - 0.06f,
                lipOut - 0.03f, reach + 0.06f,
                head - 0.02f, reach + 0.02f,
                -head * 0.5f, reach - 0.02f
            };
            return Bake("MapusRib" + variant, delegate (float x, float y)
            {
                float d = Poly(x, y, shaft, 0.045f);
                d = Mathf.Max(d, Poly(x, y, hook, 0.05f));
                // ONE broad wear flat, cut off a shoulder - iron that has been used, not iron with
                // a scratch texture on it.
                d = Mathf.Min(d, -Box(x, y, -1.12f + variant * 0.04f, -0.24f, 0.30f, 0.20f, 0.06f));
                return d;
            });
        }

        /// <summary>
        /// An EDGE SOCKET: the recessed bracket a rib is hinged into. Wide, low, square-shouldered,
        /// with the mouth the rib comes out of cut into its top. It has to look like part of the
        /// board's own frame, because four ribs with nothing at their roots are four shapes
        /// floating over a cell.
        /// </summary>
        public static Sprite Socket
        {
            get
            {
                if (socket == null)
                {
                    float[] bracket =
                    {
                        -1.00f, -1.00f,
                        1.00f, -1.00f,
                        0.94f, -0.10f,
                        0.72f, 0.52f,
                        0.34f, 0.86f,
                        -0.34f, 0.86f,
                        -0.72f, 0.52f,
                        -0.94f, -0.10f
                    };
                    socket = Bake("MapusSocket", delegate (float x, float y)
                    {
                        float d = Poly(x, y, bracket, 0.05f);
                        // The mouth: a square notch, not a round bite - it is a machined seat.
                        return Mathf.Min(d, -Box(x, y, 0f, 1.02f, 0.40f, 0.52f, 0.06f));
                    });
                }
                return socket;
            }
        }

        /// <summary>
        /// THE WARDEN SEAL: a struck lump, not a disc and not a padlock. An irregular rounded
        /// hexagon - no two sides the same length, one shoulder fuller than the rest, one flank
        /// squeezed flat where the iron bit in. Wax is the one soft thing in this effect, so this
        /// is the one shape with a generous round on it.
        /// </summary>
        public static Sprite Seal
        {
            get
            {
                if (seal == null)
                {
                    float[] lump =
                    {
                        -0.10f, 0.92f,
                        0.66f, 0.52f,
                        0.80f, -0.24f,
                        0.16f, -0.88f,
                        -0.64f, -0.62f,
                        -0.86f, 0.22f
                    };
                    seal = Bake("MapusSeal", delegate (float x, float y)
                    {
                        float d = Poly(x, y, lump, 0.14f);
                        // Where the press bit: one shallow flat, so it reads as STRUCK.
                        return Mathf.Min(d, -Box(x, y, 1.18f, -0.70f, 0.52f, 0.46f, 0.12f));
                    });
                }
                return seal;
            }
        }

        /// <summary>
        /// THE BRAND inside the seal: the relief the die left. THREE NESTED BROKEN ARCS, each
        /// broken at a different angle - guilloche, not a glyph.
        ///
        /// It is arcs because everything else turned out to be a letter. A broken ring with a bar
        /// across its gap is the letter G, exactly and unmistakably. Three strokes is a tally or a
        /// V or an X depending on the angle; a ring with anything inside it is a keyhole; a mark
        /// with a stem is a letter in some alphabet. Concentric arcs cannot be read as a character
        /// in any of them, and at cell size they are barely more than texture - which is precisely
        /// what a die pressed into wax leaves behind.
        /// </summary>
        public static Sprite Brand
        {
            get
            {
                if (brand == null)
                {
                    brand = Bake("MapusBrand", delegate (float x, float y)
                    {
                        float d = Arc(x, y, 0.86f, 0.10f, 2.1f, 1.15f);
                        d = Mathf.Max(d, Arc(x, y, 0.55f, 0.095f, -0.6f, 1.9f));
                        d = Mathf.Max(d, Arc(x, y, 0.24f, 0.09f, 3.4f, 1.5f));
                        return d;
                    });
                }
                return brand;
            }
        }

        /// <summary>
        /// ONE SUPPRESSION NOTCH: the mark a held row or column leaves on the grid edge of every
        /// cell it passes through. A shallow bracket with its ends turned in - heaviest across its
        /// middle, gone at both tips. Never an even dash (a row of those is morse code) and never a
        /// full band (that is the coloured stripe this whole layer replaces). Baked below full
        /// alpha, because it is meant to be felt rather than seen.
        /// </summary>
        public static Sprite Notch
        {
            get
            {
                if (notch == null)
                {
                    float[] bracket =
                    {
                        -0.92f, 0.34f,
                        -0.62f, -0.30f,
                        0.62f, -0.30f,
                        0.92f, 0.34f,
                        0.58f, 0.16f,
                        -0.58f, 0.16f
                    };
                    notch = Bake("MapusNotch", delegate (float x, float y)
                    {
                        return Poly(x, y, bracket, 0.06f);
                    }, 0.9f);
                }
                return notch;
            }
        }

        // =================================================================== the primitives

        private delegate float Field(float x, float y);

        /// <summary>
        /// Signed distance to a CONVEX polygon, positive inside, with <paramref name="round"/> of
        /// fillet added all the way round. Points are x,y pairs in order; the winding may be either
        /// way. This is the primitive the whole file is built on, because it is the only one that
        /// gives a FLAT run between two crisp corners - which is what forged iron is made of and
        /// what a fused circle can never be.
        /// </summary>
        private static float Poly(float x, float y, float[] points, float round)
        {
            int n = points.Length / 2;
            // Which way it is wound, so the caller does not have to care.
            float area = 0f;
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                area += points[i * 2] * points[j * 2 + 1] - points[j * 2] * points[i * 2 + 1];
            }
            float sign = area >= 0f ? 1f : -1f;
            float outside = float.NegativeInfinity;
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                float ax = points[i * 2];
                float ay = points[i * 2 + 1];
                float ex = points[j * 2] - ax;
                float ey = points[j * 2 + 1] - ay;
                float len = Mathf.Sqrt(ex * ex + ey * ey);
                if (len < 1e-6f)
                {
                    continue;
                }
                // Outward normal of this edge.
                float nx = sign * ey / len;
                float ny = -sign * ex / len;
                outside = Mathf.Max(outside, (x - ax) * nx + (y - ay) * ny);
            }
            return -outside + round;
        }

        /// <summary>Signed distance to an axis-aligned rounded box, positive inside.</summary>
        private static float Box(float x, float y, float cx, float cy, float hw, float hh,
            float round)
        {
            float dx = Mathf.Abs(x - cx) - (hw - round);
            float dy = Mathf.Abs(y - cy) - (hh - round);
            float outside = Mathf.Sqrt(Mathf.Max(dx, 0f) * Mathf.Max(dx, 0f)
                + Mathf.Max(dy, 0f) * Mathf.Max(dy, 0f));
            return -(outside + Mathf.Min(Mathf.Max(dx, dy), 0f)) + round;
        }

        /// <summary>Signed distance to a capsule between two points, positive inside.</summary>
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

        /// <summary>Signed distance to an ARC: a piece of an annulus of the given radius and half
        /// thickness, centred on <paramref name="from"/> radians and <paramref name="span"/>
        /// radians long, with rounded ends. Positive inside.</summary>
        private static float Arc(float x, float y, float radius, float half, float from,
            float span)
        {
            float len = Mathf.Sqrt(x * x + y * y);
            float wall = half - Mathf.Abs(len - radius);
            // How far round the arc this point is, wrapped into 0..2pi from its start.
            float a = Mathf.Atan2(y, x) - from;
            const float TwoPi = Mathf.PI * 2f;
            a -= Mathf.Floor(a / TwoPi) * TwoPi;
            if (a <= span)
            {
                return wall;
            }
            // Past either end: round it off against the nearer cap.
            float toEnd = Mathf.Min(a - span, TwoPi - a);
            return Mathf.Min(wall, half - toEnd * radius);
        }

        // =================================================================== the baker

        private static Sprite Bake(string name, Field field)
        {
            return Bake(name, field, 1f);
        }

        /// <summary>Rasterises a field into an alpha texture: one pixel of ramp across the contour,
        /// so the silhouette is smooth without being a soft blur - which for iron matters twice
        /// over, since a soft edge is the difference between forged metal and a shadow.</summary>
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
