// PURPOSE: "Tılsım"'s SOFT DARK MESHES - the three things in the claim that are neither the vine
// art nor the baked stain: the SHADOW TENDRILS that crawl out of the vines into the rest of the
// claimed area, the VEIL POCKETS stretched across what is left over, and the DARK STREAMS the
// darkness leaves along when the gift is taken back.
//
// WHY THEY ARE NOT MORE VINES. The vine is a nine-frame drawing and it is the hero; the honest way
// to cover more ground with it is to grow more of it, and that is exactly the pass that ended as a
// ball of snakes. What the empty parts of a claim need is not more plant, it is the plant's
// SHADOW - the residue of something that grew through here - and a shadow is allowed to be a
// simple dark curve, because nobody reads detail into a shadow.
//
// SO THESE MUST NEVER TRY TO LOOK LIKE THE ART: no green, no highlight, no leaves, no tip detail.
// A tendril that gets pretty stops being support and starts competing with the thing it exists to
// hold up. Dark teal-black, one width, soft edge, and that is all.
//
// THE SOFTNESS IS IN THE VERTICES, not in a texture and not in a blur. A ribbon is three rows of
// vertices - rim, spine, rim - with the rims at zero alpha, and a pocket is two rings round a
// centre with the outer ring at zero. That is the whole trick, it costs one white texture for the
// entire system, and it is why this layer runs on a phone.

using System.Collections.Generic;
using UnityEngine;

namespace ProjectBlock.View
{
    /// <summary>One soft dark mesh - a ribbon or a pocket. Geometry and nothing else: when it
    /// grows, how far it has got and what colour it is this frame are the view's business.
    /// </summary>
    public sealed class TalismanTendril
    {
        /// <summary>Every number these three layers are made of.</summary>
        public static class Style
        {
            // ---- THE SHADOW TENDRILS ----
            /// <summary>Dark teal-black, and it is a RESIDUE rather than a thing: it sits under
            /// the vines and over the stain, and it never catches a light.</summary>
            public static readonly Color Tendril = new Color(0.016f, 0.05f, 0.048f);

            public static float TendrilOpacity = 0.24f;

            /// <summary>Width in cells. Thin - the vine is the hero and this is the support; a
            /// fat tendril is just a badly drawn root.</summary>
            public static float TendrilWidth = 0.075f;

            /// <summary>How much of that width is gone by the tip.</summary>
            public static float TendrilTaper = 0.72f;

            /// <summary>How long one takes to crawl out.</summary>
            public static float TendrilGrow = 0.14f;

            /// <summary>How far a tendril bows off the straight line between its ends.</summary>
            public static float TendrilBow = 0.26f;

            /// <summary>A tendril may fork ONCE, and a fork may not fork. Two children at most,
            /// which is the difference between a root and a fern.</summary>
            public static float TendrilForkChance = 0.42f;

            public static int TendrilForkMax = 2;

            public static float TendrilForkAt = 0.55f;

            public static float TendrilForkScale = 0.62f;

            /// <summary>Samples along the curve. Ten is enough for a curve this short; the cost
            /// of this layer is the rebuild, not the triangles.</summary>
            public static int TendrilSteps = 10;

            // ---- THE VEIL POCKETS ----
            /// <summary>Thinner than the tendrils and much thinner than the stain: a skin
            /// stretched over a hole, not a patch sewn into it.</summary>
            public static readonly Color Veil = new Color(0.03f, 0.075f, 0.072f);

            public static float VeilOpacity = 0.15f;

            /// <summary>How far the skin sags in from the line between its anchors.</summary>
            public static float VeilSag = 0.22f;

            public static int VeilRim = 16;

            /// <summary>How far in from the rim the mesh becomes fully opaque - the soft edge.
            /// </summary>
            public static float VeilCore = 0.74f;

            public static float VeilGrow = 0.16f;

            /// <summary>The size band one is allowed to be, in cells. No giant sheet: a veil that
            /// covers a whole cell is the blob this whole system exists to avoid.</summary>
            public static float VeilMin = 0.25f;

            public static float VeilMax = 0.7f;

            // ---- THE DARK STREAMS (the unseal) ----
            /// <summary>Two to four short flows per cell, running from the patch into the vine
            /// that is pulling it. This is what makes the unseal a SUCTION rather than a fade -
            /// darkness that only fades out was never really there.</summary>
            public static float StreamWidth = 0.055f;

            public static float StreamOpacity = 0.15f;

            public static int StreamMin = 2;

            public static int StreamMax = 4;

            /// <summary>How much of the run is visible at once - a travelling stretch, not a line
            /// that appears whole.</summary>
            public static float StreamLength = 0.45f;
        }

        private GameObject go;

        private MeshFilter filter;

        private MeshRenderer body;

        private Mesh mesh;

        private static Material shared;

        private static Material Shared()
        {
            if (shared == null)
            {
                // Sprites/Default over a white texture: the mesh carries its own colour and its
                // own soft edge in the vertices, so there is nothing for a texture to do.
                shared = new Material(Shader.Find("Sprites/Default"))
                {
                    hideFlags = HideFlags.HideAndDontSave,
                    mainTexture = Texture2D.whiteTexture
                };
            }
            return shared;
        }

        public static TalismanTendril Make(Transform parent, int order)
        {
            var t = new TalismanTendril();
            t.go = new GameObject("TalismanTendril");
            t.go.transform.SetParent(parent, false);
            t.filter = t.go.AddComponent<MeshFilter>();
            t.body = t.go.AddComponent<MeshRenderer>();
            t.body.sharedMaterial = Shared();
            t.body.sortingOrder = order;
            t.mesh = new Mesh { hideFlags = HideFlags.HideAndDontSave, name = "TalismanTendril" };
            t.filter.sharedMesh = t.mesh;
            return t;
        }

        public void Order(int order)
        {
            if (body != null)
            {
                body.sortingOrder = order;
            }
        }

        public void Hide()
        {
            if (body != null)
            {
                body.enabled = false;
            }
        }

        public void Destroy()
        {
            if (mesh != null)
            {
                Object.Destroy(mesh);
                mesh = null;
            }
            if (go != null)
            {
                Object.Destroy(go);
                go = null;
            }
            filter = null;
            body = null;
        }

        // =================================================================== the ribbon

        private static readonly List<Vector3> verts = new List<Vector3>();

        private static readonly List<Color> cols = new List<Color>();

        private static readonly List<int> tris = new List<int>();

        /// <summary>A point on the curve - public because the view hangs forks and anchors off
        /// it, and two implementations of the same curve is a fork that misses its parent.
        /// </summary>
        public static Vector2 On(Vector2 a, Vector2 c, Vector2 b, float t)
        {
            float u = 1f - t;
            return u * u * a + 2f * u * t * c + t * t * b;
        }

        /// <summary>
        /// A CURVED STRIP from <paramref name="from"/> to <paramref name="to"/> along the curve,
        /// tapering toward the tip and soft on both flanks.
        ///
        /// Growing moves <paramref name="to"/> out; RETRACTING PULLS IT BACK, tip toward parent,
        /// which is the same call with the same two numbers and no second code path. A stream is
        /// this with both numbers moving together - a stretch of darkness travelling along.
        /// </summary>
        public void Ribbon(Vector2 a, Vector2 c, Vector2 b, float width, float taper,
            float from, float to, Color colour)
        {
            if (body == null || to - from < 0.015f || colour.a <= 0.002f)
            {
                Hide();
                return;
            }
            body.enabled = true;
            verts.Clear();
            cols.Clear();
            tris.Clear();
            int n = Mathf.Max(3, Style.TendrilSteps);
            for (int i = 0; i < n; i++)
            {
                float k = i / (float)(n - 1);
                float t = Mathf.Lerp(from, to, k);
                Vector2 p = On(a, c, b, t);
                Vector2 ahead = On(a, c, b, Mathf.Min(1f, t + 0.02f));
                Vector2 back = On(a, c, b, Mathf.Max(0f, t - 0.02f));
                Vector2 dir = ahead - back;
                if (dir.sqrMagnitude < 1e-8f)
                {
                    dir = Vector2.right;
                }
                dir.Normalize();
                var normal = new Vector2(-dir.y, dir.x);
                float half = width * 0.5f * (1f - taper * t);
                // The two ends close rather than being cut off square: a shadow root has no
                // blunt end, and a hard end is the one thing that says "quad".
                float fade = Mathf.Clamp01(k / 0.12f) * Mathf.Clamp01((1f - k) / 0.22f);
                fade = fade * fade * (3f - 2f * fade);
                var spine = new Color(colour.r, colour.g, colour.b, colour.a * fade);
                var rim = new Color(colour.r, colour.g, colour.b, 0f);
                verts.Add(new Vector3(p.x - normal.x * half, p.y - normal.y * half, 0f));
                verts.Add(new Vector3(p.x, p.y, 0f));
                verts.Add(new Vector3(p.x + normal.x * half, p.y + normal.y * half, 0f));
                cols.Add(rim);
                cols.Add(spine);
                cols.Add(rim);
                if (i > 0)
                {
                    int q = (i - 1) * 3;
                    tris.Add(q); tris.Add(q + 3); tris.Add(q + 1);
                    tris.Add(q + 1); tris.Add(q + 3); tris.Add(q + 4);
                    tris.Add(q + 1); tris.Add(q + 4); tris.Add(q + 2);
                    tris.Add(q + 2); tris.Add(q + 4); tris.Add(q + 5);
                }
            }
            Upload();
        }

        // =================================================================== the pocket

        /// <summary>
        /// A SKIN over a hole, hung on two to four anchors on the network.
        ///
        /// The rim is a closed curve through those anchors pulled IN toward the middle, so the
        /// shape is concave between them the way a membrane between two branches actually is -
        /// a convex blob is a bubble, and a bubble is a thing rather than a gap being covered.
        /// </summary>
        public void Pocket(Vector2[] anchors, int count, float grow, float sag, Color colour)
        {
            if (body == null || count < 3 || grow <= 0.02f || colour.a <= 0.002f)
            {
                Hide();
                return;
            }
            body.enabled = true;
            verts.Clear();
            cols.Clear();
            tris.Clear();
            Vector2 centre = Vector2.zero;
            for (int i = 0; i < count; i++)
            {
                centre += anchors[i];
            }
            centre /= count;
            var spine = new Color(colour.r, colour.g, colour.b, colour.a);
            var rim = new Color(colour.r, colour.g, colour.b, 0f);
            verts.Add(new Vector3(centre.x, centre.y, 0f));
            cols.Add(spine);
            int n = Mathf.Max(6, Style.VeilRim);
            for (int i = 0; i < n; i++)
            {
                float k = i / (float)n * count;
                int seg = Mathf.Clamp((int)k, 0, count - 1);
                float f = k - seg;
                Vector2 p0 = anchors[seg];
                Vector2 p1 = anchors[(seg + 1) % count];
                Vector2 edge = Vector2.Lerp(p0, p1, f);
                // THE SAG: pulled toward the middle hardest half way between two anchors.
                float bow = Mathf.Sin(f * Mathf.PI) * sag;
                Vector2 outer = Vector2.Lerp(edge, centre, bow);
                outer = centre + (outer - centre) * grow;
                Vector2 inner = Vector2.Lerp(outer, centre, 1f - Style.VeilCore);
                verts.Add(new Vector3(outer.x, outer.y, 0f));
                verts.Add(new Vector3(inner.x, inner.y, 0f));
                cols.Add(rim);
                cols.Add(spine);
            }
            for (int i = 0; i < n; i++)
            {
                int a = 1 + i * 2;
                int b = 1 + ((i + 1) % n) * 2;
                tris.Add(a); tris.Add(b); tris.Add(a + 1);
                tris.Add(a + 1); tris.Add(b); tris.Add(b + 1);
                tris.Add(0); tris.Add(a + 1); tris.Add(b + 1);
            }
            Upload();
        }

        private void Upload()
        {
            mesh.Clear();
            mesh.SetVertices(verts);
            mesh.SetColors(cols);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
        }
    }
}
