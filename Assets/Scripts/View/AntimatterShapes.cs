// PURPOSE: The baked shapes "Antimadde"'s annihilation is drawn with (AntimatterBlastView): soft
// light falloffs, three weights of ring, a rounded-square band that follows a cube's own outline,
// a velocity streak, a screen-edge falloff, the beam quad and the filament strip builder.
//
// Every texture is WHITE with the shape in its alpha, so the renderer colour (and the additive
// glow shader) says what colour the light is. Rings are baked with their band at a known share of
// the sprite (RingBand), so a caller asks for a RADIUS and gets it - a "size" that secretly meant a
// diameter one place and a band position another is how rings end up a tenth too big.
//
// The rings come in three weights because one band scaled to every radius is either a smear on the
// board-wide shockwave or a hairline round a single cube: RingThin is the arena's pressure edge and its
// outer hairline, RingMid the refractive bands behind it, RingLocal a cube's own wave, RingSoft a
// core's halo. A WIDE soft band at a board's radius is a violet fog over the arena - the one
// thing this event must never become - so nothing that size uses RingSoft.

using UnityEngine;

namespace ProjectBlock.View
{
    public static class AntimatterShapes
    {
        /// <summary>Where every ring sprite's band sits, as a share of the sprite's half-size.
        /// Scale a ring sprite to 2 * radius / RingBand and its band lands on radius.</summary>
        public const float RingBand = 0.9f;

        private static Sprite glow;
        private static Sprite dot;
        private static Sprite ringThin;
        private static Sprite ringMid;
        private static Sprite ringLocal;
        private static Sprite ringSoft;
        private static Sprite squareRing;
        private static Sprite squareSoft;
        private static Sprite streak;
        private static Sprite edge;
        private static Mesh beamQuad;

        /// <summary>A gaussian light falloff - the peak cores, the central core, particles.</summary>
        public static Sprite Glow
        {
            get
            {
                return glow != null ? glow : glow = Bake(64, (x, y) =>
                {
                    float d = Mathf.Sqrt(x * x + y * y);
                    return Mathf.Exp(-d * d * 5.5f) * (1f - Mathf.Clamp01((d - 0.9f) / 0.1f));
                });
            }
        }

        /// <summary>A soft filled disc with a firm middle - the dark singularity cores and the lens.
        /// </summary>
        public static Sprite Dot
        {
            get
            {
                return dot != null ? dot : dot = Bake(64, (x, y) =>
                {
                    float d = Mathf.Sqrt(x * x + y * y);
                    return 1f - Mathf.SmoothStep(0.55f, 1f, d);
                });
            }
        }

        /// <summary>The arena shockwave's pressure edge: a thin band that stays thin at a board's
        /// radius.</summary>
        public static Sprite RingThin
        {
            get { return ringThin != null ? ringThin : ringThin = BakeRing(512, 0.018f); }
        }

        /// <summary>The refractive bands just inside the shockwave's edge, and the lens's shade: thin
        /// enough at a board's radius that the wave stays a structure and never becomes a haze.</summary>
        public static Sprite RingMid
        {
            get { return ringMid != null ? ringMid : ringMid = BakeRing(512, 0.035f); }
        }

        /// <summary>A cube's own local wave - thick enough to read round one cell.</summary>
        public static Sprite RingLocal
        {
            get { return ringLocal != null ? ringLocal : ringLocal = BakeRing(128, 0.06f); }
        }

        /// <summary>A wide gaussian band - the distortion band, the outer rim, a core's halo.</summary>
        public static Sprite RingSoft
        {
            get { return ringSoft != null ? ringSoft : ringSoft = BakeRing(256, 0.13f); }
        }

        /// <summary>A rounded-square BAND along a cube's own outline: the inward ring that closes
        /// from the cube's edge on its centre, and the notice's chromatic fringe. The band sits at
        /// RingBand of the half-size, like the round rings.</summary>
        public static Sprite SquareRing
        {
            get { return squareRing != null ? squareRing : squareRing = BakeSquare(128, 0.045f); }
        }

        /// <summary>The same outline, soft - the violet haze outside the inward ring and the
        /// lavender edge the shockwave leaves on the cubes that stay.</summary>
        public static Sprite SquareSoft
        {
            get { return squareSoft != null ? squareSoft : squareSoft = BakeSquare(128, 0.11f); }
        }

        /// <summary>A soft capsule four times as long as it is wide, along +x - a luminous streak
        /// turned along its own velocity.</summary>
        public static Sprite Streak
        {
            get
            {
                return streak != null ? streak : streak = Bake(64, (x, y) =>
                {
                    float ax = Mathf.Abs(x);
                    float ay = Mathf.Abs(y) * 4f;
                    float d = Mathf.Sqrt(ax * ax + ay * ay);
                    return Mathf.Exp(-d * d * 4f) * (1f - Mathf.Clamp01((d - 0.9f) / 0.1f));
                });
            }
        }

        /// <summary>A falloff that is zero over the middle of the screen and rises only in the
        /// last few percent toward its edges - the chromatic fringe at the peak. Never a wash.</summary>
        public static Sprite ScreenEdge
        {
            get
            {
                return edge != null ? edge : edge = Bake(128, (x, y) =>
                {
                    float d = Mathf.Max(Mathf.Abs(x), Mathf.Abs(y));
                    float k = Mathf.Clamp01((d - 0.84f) / 0.16f);
                    return k * k;
                });
            }
        }

        /// <summary>The beam quad: x 0..1 from root to tip, y -0.5..0.5 across; uv.x along, uv.y
        /// across. One shared mesh for every ray, blade and column.</summary>
        public static Mesh BeamQuad
        {
            get
            {
                if (beamQuad != null)
                {
                    return beamQuad;
                }
                beamQuad = new Mesh { name = "AntimatterBeamQuad" };
                beamQuad.vertices = new[]
                {
                    new Vector3(0f, -0.5f, 0f), new Vector3(1f, -0.5f, 0f),
                    new Vector3(1f, 0.5f, 0f), new Vector3(0f, 0.5f, 0f)
                };
                beamQuad.uv = new[]
                {
                    new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f)
                };
                beamQuad.colors = new[] { Color.white, Color.white, Color.white, Color.white };
                beamQuad.triangles = new[] { 0, 2, 1, 0, 3, 2 };
                beamQuad.RecalculateBounds();
                // A beam can be scaled to several cells; bounds that grow with it keep it from
                // being culled while its quad is still on screen.
                beamQuad.bounds = new Bounds(new Vector3(0.5f, 0f, 0f), new Vector3(1f, 1f, 1f));
                return beamQuad;
            }
        }

        /// <summary>
        /// Writes a strip along <paramref name="points"/> into <paramref name="mesh"/>: uv.x runs
        /// 0..1 along it, uv.y 0..1 across, and each point's colour (with its own alpha - a filament's
        /// broken sections) goes on both of its vertices. <paramref name="width"/> is the strip's
        /// full width in the mesh's own units.
        /// </summary>
        public static void WriteStrip(Mesh mesh, Vector2[] points, Color[] colours, int count, float width,
            Vector3[] vertexBuffer, Vector2[] uvBuffer, Color[] colourBuffer, int[] triangleBuffer)
        {
            float half = width * 0.5f;
            for (int i = 0; i < count; i++)
            {
                Vector2 prev = points[Mathf.Max(0, i - 1)];
                Vector2 next = points[Mathf.Min(count - 1, i + 1)];
                Vector2 tangent = next - prev;
                if (tangent.sqrMagnitude < 1e-10f)
                {
                    tangent = Vector2.right;
                }
                tangent.Normalize();
                var normal = new Vector2(-tangent.y, tangent.x);
                vertexBuffer[i * 2] = points[i] - normal * half;
                vertexBuffer[i * 2 + 1] = points[i] + normal * half;
                float u = count > 1 ? i / (float)(count - 1) : 0f;
                uvBuffer[i * 2] = new Vector2(u, 0f);
                uvBuffer[i * 2 + 1] = new Vector2(u, 1f);
                colourBuffer[i * 2] = colours[i];
                colourBuffer[i * 2 + 1] = colours[i];
            }
            int t = 0;
            for (int i = 0; i < count - 1; i++)
            {
                int a = i * 2;
                triangleBuffer[t++] = a;
                triangleBuffer[t++] = a + 2;
                triangleBuffer[t++] = a + 1;
                triangleBuffer[t++] = a + 1;
                triangleBuffer[t++] = a + 2;
                triangleBuffer[t++] = a + 3;
            }
            mesh.Clear();
            mesh.vertices = vertexBuffer;
            mesh.uv = uvBuffer;
            mesh.colors = colourBuffer;
            mesh.triangles = triangleBuffer;
            mesh.RecalculateBounds();
        }

        private static Sprite BakeRing(int px, float halfWidth)
        {
            return Bake(px, (x, y) =>
            {
                float d = Mathf.Sqrt(x * x + y * y);
                float k = (d - RingBand) / halfWidth;
                return Mathf.Exp(-k * k) * (1f - Mathf.Clamp01((d - 0.985f) / 0.015f));
            });
        }

        private static Sprite BakeSquare(int px, float halfWidth)
        {
            // A rounded box whose outline sits at RingBand: the distance to that outline, as a
            // gaussian band either side of it.
            const float corner = 0.22f;
            return Bake(px, (x, y) =>
            {
                float bx = Mathf.Abs(x) - (RingBand - corner);
                float by = Mathf.Abs(y) - (RingBand - corner);
                float outside = new Vector2(Mathf.Max(bx, 0f), Mathf.Max(by, 0f)).magnitude;
                float inside = Mathf.Min(Mathf.Max(bx, by), 0f);
                float sd = outside + inside - corner;
                float k = sd / halfWidth;
                return Mathf.Exp(-k * k);
            });
        }

        private static Sprite Bake(int px, System.Func<float, float, float> alpha)
        {
            var tex = new Texture2D(px, px, TextureFormat.RGBA32, false);
            var pixels = new Color32[px * px];
            float half = px * 0.5f;
            for (int y = 0; y < px; y++)
            {
                for (int x = 0; x < px; x++)
                {
                    float dx = (x + 0.5f - half) / half;
                    float dy = (y + 0.5f - half) / half;
                    byte a = (byte)Mathf.RoundToInt(Mathf.Clamp01(alpha(dx, dy)) * 255f);
                    pixels[y * px + x] = new Color32(255, 255, 255, a);
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            // One world unit a side, so a localScale IS the size.
            return Sprite.Create(tex, new Rect(0, 0, px, px), new Vector2(0.5f, 0.5f), px);
        }
    }
}
