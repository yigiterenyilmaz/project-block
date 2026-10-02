// PURPOSE: The two meshes "Rüzgar" is built on.
//
// WindRibbon is ONE flow ribbon: a soft strip along a short stretch of the gust, wandering a
// little off the wind's own line (a pressure ribbon, never a beam), thin at its tail and fuller
// toward its head, closed at both ends, and BROKEN - its light gives out in a place or two along
// its length, because an unbroken pale line across a board is a laser however gently it curves.
// The soft flanks are written into the vertex alphas (rim / spine / rim), so there is no texture
// and no blur. Pooled by the view; one shared material.
//
// WindCorridor is the storm's body: one quad from the corridor's back edge to its end, drawn by
// Resources/Shaders/WindCorridor through a property block. Without that shader it is the baked
// band sprite - a lane still, with no flow in it and no front.

using System.Collections.Generic;
using ProjectBlock.Core;
using UnityEngine;

namespace ProjectBlock.View
{
    internal sealed class WindRibbon
    {
        private const int Steps = 12;

        private GameObject go;
        private MeshRenderer body;
        private Mesh mesh;

        private static readonly List<Vector3> verts = new List<Vector3>();
        private static readonly List<Color> cols = new List<Color>();
        private static readonly List<int> tris = new List<int>();

        public static WindRibbon Make(Transform parent, int order)
        {
            var r = new WindRibbon();
            r.go = new GameObject("WindRibbon");
            r.go.transform.SetParent(parent, false);
            var filter = r.go.AddComponent<MeshFilter>();
            r.body = r.go.AddComponent<MeshRenderer>();
            r.body.sharedMaterial = WindFx.GlowMesh;
            r.body.sortingOrder = order;
            r.mesh = new Mesh { hideFlags = HideFlags.HideAndDontSave, name = "WindRibbon" };
            filter.sharedMesh = r.mesh;
            r.body.enabled = false;
            return r;
        }

        public void Hide()
        {
            if (body != null)
            {
                body.enabled = false;
            }
        }

        /// <summary>
        /// Draws the ribbon from <paramref name="tail"/> to <paramref name="head"/> (world), bowed
        /// sideways by <paramref name="wander"/>(k) world units at each point k of its length.
        /// <paramref name="breaks"/> picks where its light gives out.
        /// </summary>
        public void Draw(Vector2 tail, Vector2 head, System.Func<float, float> wander, float width,
            Color colour, float breaks)
        {
            if (body == null || colour.a <= 0.002f || (head - tail).sqrMagnitude < 1e-6f)
            {
                Hide();
                return;
            }
            body.enabled = true;
            verts.Clear();
            cols.Clear();
            tris.Clear();
            Vector2 dir = (head - tail).normalized;
            var side = new Vector2(-dir.y, dir.x);
            for (int i = 0; i < Steps; i++)
            {
                float k = i / (float)(Steps - 1);
                Vector2 p = Vector2.Lerp(tail, head, k) + side * wander(k);
                // closed at both ends, fullest two thirds of the way to the head
                float fade = Mathf.Clamp01(k / 0.22f) * Mathf.Clamp01((1f - k) / 0.14f);
                fade = fade * fade * (3f - 2f * fade);
                // the break: one soft gap that sits somewhere in the ribbon's back half
                float gap = 1f - 0.85f * Mathf.Exp(-Mathf.Pow((k - (0.25f + 0.35f * breaks)) / 0.07f, 2f));
                float half = width * 0.5f * Mathf.Lerp(0.35f, 1f, k);
                var spine = new Color(colour.r, colour.g, colour.b, colour.a * fade * gap);
                var rim = new Color(colour.r, colour.g, colour.b, 0f);
                verts.Add(new Vector3(p.x - side.x * half, p.y - side.y * half, 0f));
                verts.Add(new Vector3(p.x, p.y, 0f));
                verts.Add(new Vector3(p.x + side.x * half, p.y + side.y * half, 0f));
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
            mesh.Clear();
            mesh.SetVertices(verts);
            mesh.SetColors(cols);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
        }
    }

    /// <summary>The storm's body. See the file header.</summary>
    internal sealed class WindCorridor
    {
        /// <summary>The quad is this many cells wide - the band, and room for its soft edges.</summary>
        public const float QuadWidth = 4f;

        public struct Look
        {
            public Color Tint;
            public Color Edge;
            public float BodyAlpha;
            public float EdgeAlpha;
            public float Doublet;
            public float Flow;
            public float Front;
            public float FrontAlpha;
            public float Reveal;
            public float Squeeze;
            public float Fade;
        }

        private GameObject go;
        private MeshRenderer body;
        private SpriteRenderer fallback;
        private MaterialPropertyBlock block;

        private static Mesh quad;

        private static readonly int TintId = Shader.PropertyToID("_Tint");
        private static readonly int EdgeColorId = Shader.PropertyToID("_EdgeColor");
        private static readonly int LenId = Shader.PropertyToID("_LenCells");
        private static readonly int WidId = Shader.PropertyToID("_WidCells");
        private static readonly int HalfId = Shader.PropertyToID("_HalfBand");
        private static readonly int BodyId = Shader.PropertyToID("_BodyAlpha");
        private static readonly int EdgeId = Shader.PropertyToID("_EdgeAlpha");
        private static readonly int DoubletId = Shader.PropertyToID("_Doublet");
        private static readonly int FlowId = Shader.PropertyToID("_Flow");
        private static readonly int FrontId = Shader.PropertyToID("_Front");
        private static readonly int FrontAlphaId = Shader.PropertyToID("_FrontAlpha");
        private static readonly int RevealId = Shader.PropertyToID("_Reveal");
        private static readonly int SqueezeId = Shader.PropertyToID("_Squeeze");
        private static readonly int FadeId = Shader.PropertyToID("_Fade");

        public static WindCorridor Make(Transform parent, int order)
        {
            var c = new WindCorridor();
            c.go = new GameObject("WindCorridor");
            c.go.transform.SetParent(parent, false);
            c.block = new MaterialPropertyBlock();
            if (WindFx.Corridor != null)
            {
                var filter = c.go.AddComponent<MeshFilter>();
                filter.sharedMesh = Quad();
                c.body = c.go.AddComponent<MeshRenderer>();
                c.body.sharedMaterial = WindFx.Corridor;
                c.body.sortingOrder = order;
                c.body.enabled = false;
            }
            else
            {
                c.fallback = c.go.AddComponent<SpriteRenderer>();
                c.fallback.sprite = WindShapes.Band;
                c.fallback.sortingOrder = order;
                c.fallback.enabled = false;
            }
            return c;
        }

        public void Hide()
        {
            if (body != null)
            {
                body.enabled = false;
            }
            if (fallback != null)
            {
                fallback.enabled = false;
            }
        }

        /// <summary>
        /// Lays the corridor from <paramref name="back"/> (the world point half a cell behind the
        /// gust's start) along <paramref name="angle"/>, <paramref name="lengthCells"/> long.
        /// </summary>
        public void Set(Vector2 back, float angle, float lengthCells, float cell, Look look)
        {
            if (lengthCells <= 0.01f || look.Fade <= 0.002f)
            {
                Hide();
                return;
            }
            Transform t = go.transform;
            t.rotation = Quaternion.Euler(0f, 0f, angle);
            if (body != null)
            {
                body.enabled = true;
                t.position = new Vector3(back.x, back.y, 0f);
                t.localScale = new Vector3(lengthCells * cell, QuadWidth * cell, 1f);
                block.SetColor(TintId, look.Tint);
                block.SetColor(EdgeColorId, look.Edge);
                block.SetFloat(LenId, lengthCells);
                block.SetFloat(WidId, QuadWidth);
                block.SetFloat(HalfId, WindGust.Width * 0.5f);
                block.SetFloat(BodyId, look.BodyAlpha);
                block.SetFloat(EdgeId, look.EdgeAlpha);
                block.SetFloat(DoubletId, look.Doublet);
                block.SetFloat(FlowId, look.Flow);
                block.SetFloat(FrontId, look.Front);
                block.SetFloat(FrontAlphaId, look.FrontAlpha);
                block.SetFloat(RevealId, look.Reveal);
                block.SetFloat(SqueezeId, look.Squeeze);
                block.SetFloat(FadeId, look.Fade);
                body.SetPropertyBlock(block);
                return;
            }
            // No shader: the lane alone, up to where it has been revealed.
            float shown = Mathf.Clamp(look.Reveal, 0f, lengthCells);
            if (shown <= 0.05f)
            {
                fallback.enabled = false;
                return;
            }
            fallback.enabled = true;
            Vector2 dir = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
            Vector2 mid = back + dir * (shown * 0.5f * cell);
            t.position = new Vector3(mid.x, mid.y, 0f);
            t.localScale = new Vector3(shown * cell, WindGust.Width * cell * (1f - 0.12f * look.Squeeze), 1f);
            fallback.color = WindFx.Tint(look.Tint, look.BodyAlpha * 2.2f * look.Fade);
        }

        /// <summary>A unit quad from x 0..1, y -0.5..0.5, uv 0..1 - so its scale IS its size and
        /// its origin is the corridor's back edge.</summary>
        private static Mesh Quad()
        {
            if (quad == null)
            {
                quad = new Mesh { hideFlags = HideFlags.HideAndDontSave, name = "WindCorridorQuad" };
                quad.SetVertices(new List<Vector3>
                {
                    new Vector3(0f, -0.5f, 0f), new Vector3(1f, -0.5f, 0f),
                    new Vector3(1f, 0.5f, 0f), new Vector3(0f, 0.5f, 0f)
                });
                quad.SetUVs(0, new List<Vector2>
                {
                    new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f)
                });
                quad.SetTriangles(new[] { 0, 2, 1, 0, 3, 2 }, 0);
                quad.RecalculateBounds();
            }
            return quad;
        }
    }
}
