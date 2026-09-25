using UnityEngine;

namespace Tabletop.Presentation
{
    /// <summary>Procedural meshes that Unity's primitives lack (cones, prisms, reel drums, curved bands).</summary>
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

        private static Mesh _drum;
        private static readonly System.Collections.Generic.Dictionary<string, Mesh> _arcs = new System.Collections.Generic.Dictionary<string, Mesh>();

        /// <summary>
        /// Eight-sided reel drum (one face per reel face), flat shaded. Axis along x, length 1 (x -0.5..0.5),
        /// apothem 1 (distance from axis to each face). Face k's outward normal points at angle k * 45 degrees
        /// around +x, starting at +y (k = 0) and turning toward +z.
        /// </summary>
        public static Mesh Drum
        {
            get
            {
                if (_drum != null) return _drum;
                const int n = 8;
                float r = 1f / Mathf.Cos(Mathf.PI / n); // corner radius for apothem 1
                var verts = new System.Collections.Generic.List<Vector3>();
                var tris = new System.Collections.Generic.List<int>();
                Vector3 Corner(float deg, float x) { float a = deg * Mathf.Deg2Rad; return new Vector3(x, Mathf.Cos(a) * r, Mathf.Sin(a) * r); }
                for (int k = 0; k < n; k++)
                {
                    float a0 = k * 45f - 22.5f, a1 = k * 45f + 22.5f;
                    int i = verts.Count;
                    verts.Add(Corner(a0, -0.5f)); verts.Add(Corner(a1, -0.5f)); verts.Add(Corner(a1, 0.5f)); verts.Add(Corner(a0, 0.5f));
                    tris.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
                }
                foreach (float x in new[] { -0.5f, 0.5f })
                {
                    int c = verts.Count;
                    verts.Add(new Vector3(x, 0, 0));
                    for (int k = 0; k < n; k++) verts.Add(Corner(k * 45f - 22.5f, x));
                    for (int k = 0; k < n; k++)
                    {
                        int p0 = c + 1 + k, p1 = c + 1 + (k + 1) % n;
                        // Outward normal = cross(b - a, c - a): the x = -0.5 cap must face -x, the +0.5 cap +x.
                        if (x < 0) tris.AddRange(new[] { c, p1, p0 }); else tris.AddRange(new[] { c, p0, p1 });
                    }
                }
                _drum = new Mesh { name = "Drum8", vertices = verts.ToArray(), triangles = tris.ToArray() };
                _drum.RecalculateNormals();
                _drum.RecalculateBounds();
                return _drum;
            }
        }

        /// <summary>
        /// A flat curved band (annulus sector) lying on y = 0..height, centred on the origin. Angles are degrees measured
        /// from +z toward +x, so an arc from -90 to 90 bulges toward +z. Cached per shape.
        /// </summary>
        public static Mesh Arc(float innerRadius, float outerRadius, float fromDeg, float toDeg, float height, int segments = 24)
        {
            string key = innerRadius + "|" + outerRadius + "|" + fromDeg + "|" + toDeg + "|" + height + "|" + segments;
            if (_arcs.TryGetValue(key, out var cached)) return cached;
            var verts = new System.Collections.Generic.List<Vector3>();
            var tris = new System.Collections.Generic.List<int>();
            Vector3 P(float deg, float rad, float y) { float a = deg * Mathf.Deg2Rad; return new Vector3(Mathf.Sin(a) * rad, y, Mathf.Cos(a) * rad); }
            void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d) { int i = verts.Count; verts.Add(a); verts.Add(b); verts.Add(c); verts.Add(d); tris.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 }); }
            bool full = Mathf.Abs(toDeg - fromDeg) >= 359.9f;
            for (int s = 0; s < segments; s++)
            {
                float a0 = Mathf.Lerp(fromDeg, toDeg, s / (float)segments), a1 = Mathf.Lerp(fromDeg, toDeg, (s + 1) / (float)segments);
                Quad(P(a0, innerRadius, height), P(a0, outerRadius, height), P(a1, outerRadius, height), P(a1, innerRadius, height)); // top
                Quad(P(a0, outerRadius, 0), P(a1, outerRadius, 0), P(a1, outerRadius, height), P(a0, outerRadius, height));         // outer wall
                Quad(P(a1, innerRadius, 0), P(a0, innerRadius, 0), P(a0, innerRadius, height), P(a1, innerRadius, height));         // inner wall
            }
            if (!full)
            {
                Quad(P(fromDeg, innerRadius, 0), P(fromDeg, outerRadius, 0), P(fromDeg, outerRadius, height), P(fromDeg, innerRadius, height));
                Quad(P(toDeg, outerRadius, 0), P(toDeg, innerRadius, 0), P(toDeg, innerRadius, height), P(toDeg, outerRadius, height));
            }
            var mesh = new Mesh { name = "Arc", vertices = verts.ToArray(), triangles = tris.ToArray() };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            _arcs[key] = mesh;
            return mesh;
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
