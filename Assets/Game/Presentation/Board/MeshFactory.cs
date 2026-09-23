using UnityEngine;

namespace Tabletop.Presentation
{
    /// <summary>Tiny procedural meshes that Unity's primitives lack (placeholder art only).</summary>
    public static class MeshFactory
    {
        private static Mesh _cone;

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
