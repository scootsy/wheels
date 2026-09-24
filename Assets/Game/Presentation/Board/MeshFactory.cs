using UnityEngine;

namespace Tabletop.Presentation
{
    /// <summary>Tiny procedural meshes that Unity's primitives lack (placeholder art only).</summary>
    public static class MeshFactory
    {
        private static Mesh _cone;
        private static Mesh _prism;

        /// <summary>Unit triangular prism (roof): 1 wide (x), 1 tall (y, ridge at top), 1 deep (z), base centered at origin.</summary>
        public static Mesh Prism
        {
            get
            {
                if (_prism != null) return _prism;
                var l0 = new Vector3(-0.5f, 0, -0.5f); var r0 = new Vector3(0.5f, 0, -0.5f); var t0 = new Vector3(0, 1, -0.5f);
                var l1 = new Vector3(-0.5f, 0, 0.5f); var r1 = new Vector3(0.5f, 0, 0.5f); var t1 = new Vector3(0, 1, 0.5f);
                // Separate vertices per face for flat shading.
                var verts = new[]
                {
                    l0, t0, r0,            // front
                    r1, t1, l1,            // back
                    l0, l1, t1, t0,        // left slope
                    r0, t0, t1, r1,        // right slope
                    l0, r0, r1, l1,        // bottom
                };
                var tris = new[]
                {
                    0, 1, 2,
                    3, 4, 5,
                    6, 7, 8, 6, 8, 9,
                    10, 11, 12, 10, 12, 13,
                    14, 16, 15, 14, 17, 16,
                };
                _prism = new Mesh { name = "Prism", vertices = verts, triangles = tris };
                _prism.RecalculateNormals();
                _prism.RecalculateBounds();
                return _prism;
            }
        }

        /// <summary>Unit cone: base radius 0.5 at y = 0, apex at y = 1.</summary>
        public static Mesh Cone
        {
            get
            {
                if (_cone != null) return _cone;
                const int n = 16;
                var verts = new Vector3[n * 2 + 2];
                var tris = new int[n * 6];
                for (int i = 0; i < n; i++)
                {
                    float a = i * Mathf.PI * 2f / n;
                    var p = new Vector3(Mathf.Cos(a) * 0.5f, 0, Mathf.Sin(a) * 0.5f);
                    verts[i] = p;            // side ring
                    verts[n + i] = p;        // base ring
                }
                verts[2 * n] = new Vector3(0, 1, 0);   // apex
                verts[2 * n + 1] = Vector3.zero;       // base center
                int t = 0;
                for (int i = 0; i < n; i++)
                {
                    int j = (i + 1) % n;
                    tris[t++] = i; tris[t++] = 2 * n; tris[t++] = j;
                    tris[t++] = n + i; tris[t++] = n + j; tris[t++] = 2 * n + 1;
                }
                _cone = new Mesh { name = "Cone", vertices = verts, triangles = tris };
                _cone.RecalculateNormals();
                _cone.RecalculateBounds();
                return _cone;
            }
        }
    }
}
