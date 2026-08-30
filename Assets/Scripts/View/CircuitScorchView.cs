// PURPOSE: What the overload LEAVES BEHIND. The drawn blue burn is the event; this is the
// evidence - a dark, sooty, feathered mark lying along the route the cable took, which holds for a
// moment and then heals back into the board.
//
// IT IS NOT A BLACK LINE. A hard dark stroke down the middle of the board reads as a drawn shape,
// not as damage. So the mark is two mesh strips with soft cross-sections - a narrow char core and
// a wide, very faint soot edge - and both are made ragged along their length by noise, so the
// width and the darkness wander the way a real burn does. Nothing here has a defined edge.
//
// IT FADES IN LAYERS. Everything fading together is what makes a decal look like a decal being
// switched off. Soot dissolves first and fastest, the char core outlasts it, and the very last
// thing on the board is a thin dark thread going out. That order is the whole reason the mark
// feels like it is cooling rather than being deleted.
//
// IT IS CONTINUOUS. It runs on the cable's OWN resampled route, so it turns corners exactly as the
// cable did, with no seam or segmentation - the mark and the thing that made it share a path.

using System.Collections.Generic;
using UnityEngine;

namespace ProjectBlock.View
{
    /// <summary>The burn mark left down a circuit. Begin it, then it runs itself out.</summary>
    public sealed class CircuitScorchView : MonoBehaviour
    {
        // =================================================================== TUNING
        public static class Style
        {
            /// <summary>Half the width of the deepest burn, in CELLS. Narrow: this is the part
            /// that was actually under the cable.</summary>
            public static float CharHalfWidth = 0.105f;

            /// <summary>Half the width of the soot around it. Wide and nearly invisible - it is
            /// what stops the mark having an edge, and it is also the whole of the board's
            /// reaction to the burn. Anything more than this starts painting cells.</summary>
            public static float SootHalfWidth = 0.38f;

            public static Color CharColor = new Color(0.020f, 0.018f, 0.016f);

            public static Color SootColor = new Color(0.055f, 0.050f, 0.045f);

            /// <summary>Peak darkness of each layer. Still short of opaque - the board has to
            /// stay readable THROUGH the mark - but only just. What keeps this from reading as a
            /// drawn black line is the soft cross-section and the raggedness along its length,
            /// NOT how pale it is, so the mark is allowed to be genuinely dark.</summary>
            public static float CharAlpha = 0.82f;

            public static float SootAlpha = 0.30f;

            /// <summary>How much the mark's width and darkness wander along its length. Without
            /// these it is a ruled stroke, which is exactly what a burn is not.</summary>
            public static float WidthNoise = 0.46f;

            public static float AlphaNoise = 0.45f;

            /// <summary>Noise periods per cell. Two of them, one coarse and one fine, so the
            /// raggedness has more than one size to it.</summary>
            public static float NoiseScale = 3.7f;

            public static float NoiseDetail = 11.3f;

            /// <summary>How long a point on the mark takes to darken once the burn has passed it.
            /// Set to the time from the rupture to the END of the sheet, so the mark reaches full
            /// darkness exactly as the energy goes out. That is the transition worth having: the
            /// fire is on top of the damage the whole way, and what is underneath is only revealed
            /// as the fire withdraws - rather than a finished decal popping in behind it.</summary>
            public static float AppearSeconds = 0.45f;

            /// <summary>How much darker a corner burns than a straight run. A bend holds the heat
            /// longer, and it is also where the eye goes on a route.</summary>
            public static float CornerBoost = 0.30f;

            /// <summary>The turn per route sample, in degrees, at which that boost is fully on.</summary>
            public static float CornerTurnFull = 15f;

            /// <summary>How long the finished mark sits at full darkness before it starts to
            /// go. This is the beat that lets the player actually SEE what happened.</summary>
            public static float HoldSeconds = 1.10f;

            /// <summary>The layered fade. Soot lifts first; the char core is still there after
            /// it, and is the last thing to leave the board.</summary>
            public static float SootFadeSeconds = 1.70f;

            public static float CharFadeSeconds = 2.90f;
        }

        /// <summary>On the board itself: above the surface plate, under the ambient wash, the
        /// cable and everything the overload draws. A mark is part of the floor.</summary>
        private const int SootOrder = 1;

        private const int CharOrder = 2;

        // =================================================================== cross sections

        private static Texture2D charTex;

        private static Texture2D sootTex;

        private static Material charMat;

        private static Material sootMat;

        /// <summary>The char end-on: dark almost all the way across, then a quick soft shoulder.
        /// A burn has a floor to it, unlike a glow, which is why this is a plateau and not a
        /// bell.</summary>
        private static Texture2D CharTex()
        {
            if (charTex == null)
            {
                charTex = Bake(64, delegate (float v)
                {
                    return Mathf.Clamp01(1f - Mathf.Pow(v, 2.6f)) * Mathf.Exp(-0.9f * v * v);
                });
            }
            return charTex;
        }

        /// <summary>The soot end-on: no plateau at all, just falloff reaching zero at the edge,
        /// so the mark never ends anywhere the eye can find.</summary>
        private static Texture2D SootTex()
        {
            if (sootTex == null)
            {
                sootTex = Bake(64, delegate (float v)
                {
                    return Mathf.Max(0f, Mathf.Exp(-2.15f * v * v) - 0.108f) * 1.121f;
                });
            }
            return sootTex;
        }

        private static Texture2D Bake(int h, System.Func<float, float> profile)
        {
            const int w = 4;
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                float v = Mathf.Abs((y + 0.5f) / h - 0.5f) * 2f;
                var a = (byte)Mathf.RoundToInt(Mathf.Clamp01(profile(v)) * 255f);
                for (int x = 0; x < w; x++)
                {
                    px[y * w + x] = new Color32(255, 255, 255, a);
                }
            }
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.hideFlags = HideFlags.HideAndDontSave;
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.SetPixels32(px);
            tex.Apply(false, false);
            return tex;
        }

        private static Material Mat(ref Material slot, Texture2D tex)
        {
            if (slot == null)
            {
                slot = new Material(Shader.Find("Sprites/Default"));
                slot.hideFlags = HideFlags.HideAndDontSave;
                slot.mainTexture = tex;
            }
            return slot;
        }

        // =================================================================== the two strips

        private sealed class Strip
        {
            public Mesh Mesh;
            public Color[] Cols;
            public Color Tone;
            public float Alpha;
            public float FadeSeconds;
        }

        private Strip charStrip;

        private Strip sootStrip;

        /// <summary>When each vertex pair's own darkening starts, in seconds from Begin.</summary>
        private float[] revealAt = new float[0];

        /// <summary>Per-vertex-pair darkness multiplier, so the mark is uneven along its run.</summary>
        private float[] weight = new float[0];

        private float clock;

        private bool live;

        public bool Running
        {
            get { return live; }
        }

        // =================================================================== driving it

        /// <summary>
        /// Lays a fresh mark along <paramref name="route"/>. <paramref name="sweepSeconds"/> is how
        /// long the burn takes to cross the whole cable, so the mark darkens in the same order and
        /// at the same speed the failure travelled - it is the failure's own footprint.
        /// </summary>
        public void Begin(IReadOnlyList<Vector2> route, IReadOnlyList<float> routeAt,
            float routeLength, float cell, float sweepSeconds)
        {
            Clear();
            int n = route != null ? route.Count : 0;
            if (n < 2 || routeLength <= 0f || cell <= 0f)
            {
                return;
            }

            revealAt = new float[n];
            weight = new float[n];
            charStrip = BuildStrip(route, routeAt, n, cell, Style.CharHalfWidth,
                Mat(ref charMat, CharTex()), CharOrder, Style.CharColor, Style.CharAlpha,
                Style.CharFadeSeconds);
            sootStrip = BuildStrip(route, routeAt, n, cell, Style.SootHalfWidth,
                Mat(ref sootMat, SootTex()), SootOrder, Style.SootColor, Style.SootAlpha,
                Style.SootFadeSeconds);

            for (int i = 0; i < n; i++)
            {
                revealAt[i] = routeAt[i] / routeLength * Mathf.Max(sweepSeconds, 0f);
                float x = routeAt[i] / cell;
                float ragged = Noise(x * Style.NoiseScale) * 0.68f
                    + Noise(x * Style.NoiseDetail) * 0.32f;
                float turn = i == 0 ? 0f
                    : Mathf.Abs(Mathf.DeltaAngle(HeadingAt(route, i - 1), HeadingAt(route, i)));
                float corner = 1f + Style.CornerBoost
                    * Mathf.Clamp01(turn / Mathf.Max(Style.CornerTurnFull, 0.01f));
                weight[i] = Mathf.Clamp01((1f + ragged * Style.AlphaNoise) * corner);
            }

            clock = 0f;
            live = true;
            Paint();
        }

        private Strip BuildStrip(IReadOnlyList<Vector2> route, IReadOnlyList<float> routeAt, int n,
            float cell, float halfCells, Material material, int order, Color tone, float alpha,
            float fadeSeconds)
        {
            var go = new GameObject(order == CharOrder ? "Char" : "Soot");
            go.transform.SetParent(transform, false);
            var filter = go.AddComponent<MeshFilter>();
            MeshRenderer r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.sortingOrder = order;

            var verts = new Vector3[n * 2];
            var uvs = new Vector2[n * 2];
            var tris = new int[(n - 1) * 6];
            float half = halfCells * cell;

            for (int i = 0; i < n; i++)
            {
                Vector2 dir = i == 0
                    ? (route[1] - route[0]).normalized
                    : i >= n - 1
                        ? (route[n - 1] - route[n - 2]).normalized
                        : (route[i + 1] - route[i - 1]).normalized;
                var normal = new Vector2(-dir.y, dir.x);

                // The width wanders too, not just the darkness: a burn that is exactly as wide
                // everywhere is a stroke, whatever its edges look like.
                float x = routeAt[i] / cell;
                float wobble = 1f + (Noise(x * Style.NoiseScale + 17.3f) * 0.7f
                    + Noise(x * Style.NoiseDetail + 4.1f) * 0.3f) * Style.WidthNoise;
                float w = half * wobble;

                verts[i * 2] = route[i] - normal * w;
                verts[i * 2 + 1] = route[i] + normal * w;
                uvs[i * 2] = new Vector2(x, 0f);
                uvs[i * 2 + 1] = new Vector2(x, 1f);
            }
            for (int i = 0; i < n - 1; i++)
            {
                int t = i * 6;
                int a = i * 2;
                tris[t] = a; tris[t + 1] = a + 2; tris[t + 2] = a + 1;
                tris[t + 3] = a + 1; tris[t + 4] = a + 2; tris[t + 5] = a + 3;
            }

            var mesh = new Mesh { hideFlags = HideFlags.HideAndDontSave, name = go.name };
            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.triangles = tris;
            filter.sharedMesh = mesh;

            return new Strip
            {
                Mesh = mesh,
                Cols = new Color[n * 2],
                Tone = tone,
                Alpha = alpha,
                FadeSeconds = fadeSeconds
            };
        }

        private void Update()
        {
            if (!live)
            {
                return;
            }
            clock += Time.deltaTime;
            Paint();
        }

        private void Paint()
        {
            bool any = false;
            any |= PaintStrip(charStrip);
            any |= PaintStrip(sootStrip);
            if (!any)
            {
                Clear();
            }
        }

        private bool PaintStrip(Strip s)
        {
            if (s == null)
            {
                return false;
            }
            // One fade for the whole strip, but a different one per strip - that difference IS
            // the layering. Soot has gone entirely while the char is still on its way out.
            float leaves = Style.AppearSeconds + Style.HoldSeconds + s.FadeSeconds;
            bool alive = false;
            int n = s.Cols.Length / 2;
            for (int i = 0; i < n; i++)
            {
                float t = clock - revealAt[i];
                float k;
                if (t <= 0f)
                {
                    // Not darkened YET is not the same as finished: without this the strip reports
                    // itself dead on the very frame it is built - every vertex is still waiting -
                    // and tears itself down before the mark has appeared at all.
                    k = 0f;
                    alive = true;
                }
                else if (t < Style.AppearSeconds)
                {
                    k = t / Style.AppearSeconds;
                    alive = true;
                }
                else if (t < Style.AppearSeconds + Style.HoldSeconds)
                {
                    k = 1f;
                    alive = true;
                }
                else
                {
                    float f = (t - Style.AppearSeconds - Style.HoldSeconds) / s.FadeSeconds;
                    // Eased out, so the last of the mark lingers instead of walking to zero.
                    k = f >= 1f ? 0f : (1f - f) * (1f - f);
                    if (t < leaves)
                    {
                        alive = true;
                    }
                }

                Color c = s.Tone;
                c.a = s.Alpha * k * weight[i];
                s.Cols[i * 2] = c;
                s.Cols[i * 2 + 1] = c;
                if (t > 0f)
                {
                    alive |= k > 0.001f;
                }
            }
            s.Mesh.colors = s.Cols;
            return alive;
        }

        /// <summary>How long a mark lives once it has been laid, so a caller can plan round it.</summary>
        public static float Duration
        {
            get { return Style.AppearSeconds + Style.HoldSeconds + Style.CharFadeSeconds; }
        }

        public void Clear()
        {
            live = false;
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Destroy(transform.GetChild(i).gameObject);
            }
            if (charStrip != null && charStrip.Mesh != null)
            {
                Destroy(charStrip.Mesh);
            }
            if (sootStrip != null && sootStrip.Mesh != null)
            {
                Destroy(sootStrip.Mesh);
            }
            charStrip = null;
            sootStrip = null;
        }

        private void OnDestroy()
        {
            Clear();
        }

        private static float HeadingAt(IReadOnlyList<Vector2> route, int i)
        {
            int j = Mathf.Min(i + 1, route.Count - 1);
            Vector2 d = route[j] - route[Mathf.Max(j - 1, 0)];
            return Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
        }

        // =================================================================== noise

        /// <summary>Smooth value noise in -1..1. Cheap and repeatable, which is all the
        /// raggedness needs.</summary>
        private static float Noise(float x)
        {
            int i = Mathf.FloorToInt(x);
            float f = x - i;
            f = f * f * (3f - 2f * f);
            return Mathf.Lerp(Hash(i), Hash(i + 1), f);
        }

        private static float Hash(int i)
        {
            int n = (i << 13) ^ i;
            return 1f - ((n * (n * n * 15731 + 789221) + 1376312589) & 0x7fffffff)
                / 1073741824f;
        }
    }
}
