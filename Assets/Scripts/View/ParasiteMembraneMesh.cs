// PURPOSE: The MESH "Parazit"'s membrane is drawn on - a small subdivided quad, built once and
// shared by every host.
//
// It is a mesh rather than a sprite because of ONE requirement: the idle is the cube trying to push
// its way OUT of the wrap, and the parasite pressing it back down. That needs a LOCAL bulge - the
// membrane lifting in one region while the rest of it stays put - and a sprite quad has four
// corners, so the only deformation it can express is the whole thing scaling. A 6x6 grid gives the
// vertex shader somewhere to put a bulge.
//
// The grid also carries the membrane's own SHAPE in its UVs, so the silhouette is an irregular
// organic patch (the shader's mask) rather than the square the mesh actually is. Nothing here is
// per-host: one mesh, one material, and everything that differs between hosts rides in a
// MaterialPropertyBlock.

using UnityEngine;

namespace ProjectBlock.View
{
    /// <summary>The shared subdivided quad the membrane is drawn on.</summary>
    public static class ParasiteMembraneMesh
    {
        /// <summary>Cells a side. Enough for a bulge to be local and smooth; small enough that a
        /// board full of hosts is nothing.</summary>
        public const int Subdivisions = 6;

        private static Mesh mesh;

        /// <summary>A unit quad (-0.5..0.5) subdivided into a grid, with UVs 0..1. Built once.
        /// </summary>
        public static Mesh Quad
        {
            get
            {
                if (mesh == null)
                {
                    mesh = Build();
                }
                return mesh;
            }
        }

        private static Mesh Build()
        {
            int n = Subdivisions;
            int side = n + 1;
            var vertices = new Vector3[side * side];
            var uvs = new Vector2[side * side];
            var triangles = new int[n * n * 6];
            for (int y = 0; y <= n; y++)
            {
                for (int x = 0; x <= n; x++)
                {
                    int i = y * side + x;
                    float u = x / (float)n;
                    float v = y / (float)n;
                    vertices[i] = new Vector3(u - 0.5f, v - 0.5f, 0f);
                    uvs[i] = new Vector2(u, v);
                }
            }
            int t = 0;
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    int i = y * side + x;
                    triangles[t++] = i;
                    triangles[t++] = i + side;
                    triangles[t++] = i + 1;
                    triangles[t++] = i + 1;
                    triangles[t++] = i + side;
                    triangles[t++] = i + side + 1;
                }
            }
            var built = new Mesh { name = "ParasiteMembrane" };
            built.hideFlags = HideFlags.HideAndDontSave;
            built.vertices = vertices;
            built.uv = uvs;
            built.triangles = triangles;
            built.RecalculateBounds();
            return built;
        }
    }
}
