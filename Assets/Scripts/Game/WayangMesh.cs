using System.Collections.Generic;
using UnityEngine;

namespace Pamor
{
    /// <summary>
    /// Turns a 2D outline into a thin cut-out slab, like a carved wayang kulit leather figure.
    /// Outlines are simple polygons (no holes) in figure units: about 1 unit tall, facing +X.
    /// Meshes are cached per outline, so every piece shares them.
    /// </summary>
    public static class WayangMesh
    {
        const float Thickness = 0.035f;
        static readonly Dictionary<Vector2[], Mesh> cache = new Dictionary<Vector2[], Mesh>();

        public static Mesh Get(Vector2[] outline)
        {
            if (cache.TryGetValue(outline, out var m) && m != null) return m;
            m = Build(outline);
            cache[outline] = m;
            return m;
        }

        static Mesh Build(Vector2[] outline)
        {
            var pts = new List<Vector2>(outline);
            if (SignedArea(pts) < 0) pts.Reverse(); // counter-clockwise from here on
            var tris = Triangulate(pts);

            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var indices = new List<int>();
            float hz = Thickness * 0.5f;

            // Front (-Z) and back (+Z) faces.
            foreach (var (z, outward) in new[] { (-hz, Vector3.back), (hz, Vector3.forward) })
                for (int t = 0; t < tris.Count; t += 3)
                    AddTriangle(verts, normals, indices, outward,
                        new Vector3(pts[tris[t]].x, pts[tris[t]].y, z),
                        new Vector3(pts[tris[t + 1]].x, pts[tris[t + 1]].y, z),
                        new Vector3(pts[tris[t + 2]].x, pts[tris[t + 2]].y, z));

            // Side walls, with outward normals from the counter-clockwise edges.
            for (int i = 0; i < pts.Count; i++)
            {
                var a = pts[i];
                var b = pts[(i + 1) % pts.Count];
                var d = (b - a).normalized;
                var outward = new Vector3(d.y, -d.x, 0f);
                var fa = new Vector3(a.x, a.y, -hz); var fb = new Vector3(b.x, b.y, -hz);
                var ba = new Vector3(a.x, a.y, hz); var bb = new Vector3(b.x, b.y, hz);
                AddTriangle(verts, normals, indices, outward, fa, fb, bb);
                AddTriangle(verts, normals, indices, outward, fa, bb, ba);
            }

            var mesh = new Mesh { name = "Wayang" };
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetTriangles(indices, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Adds a flat-shaded triangle wound so that its front faces along the given outward normal.</summary>
        static void AddTriangle(List<Vector3> verts, List<Vector3> normals, List<int> indices, Vector3 outward, Vector3 a, Vector3 b, Vector3 c)
        {
            // Unity treats Cross(b - a, c - a) as the face normal of a front-facing triangle.
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), outward) < 0f) { var t = b; b = c; c = t; }
            int i = verts.Count;
            verts.Add(a); verts.Add(b); verts.Add(c);
            normals.Add(outward); normals.Add(outward); normals.Add(outward);
            indices.Add(i); indices.Add(i + 1); indices.Add(i + 2);
        }

        static float SignedArea(List<Vector2> p)
        {
            float a = 0f;
            for (int i = 0; i < p.Count; i++) { var u = p[i]; var v = p[(i + 1) % p.Count]; a += u.x * v.y - v.x * u.y; }
            return a * 0.5f;
        }

        /// <summary>Ear-clipping triangulation of a counter-clockwise simple polygon.</summary>
        static List<int> Triangulate(List<Vector2> p)
        {
            var result = new List<int>();
            var idx = new List<int>();
            for (int i = 0; i < p.Count; i++) idx.Add(i);
            int guard = 0;
            while (idx.Count > 3 && guard++ < 5000)
            {
                bool clipped = false;
                for (int i = 0; i < idx.Count; i++)
                {
                    int ia = idx[(i - 1 + idx.Count) % idx.Count], ib = idx[i], ic = idx[(i + 1) % idx.Count];
                    Vector2 a = p[ia], b = p[ib], c = p[ic];
                    if (Cross(b - a, c - b) <= 0f) continue; // reflex corner: not an ear
                    bool inside = false;
                    foreach (int j in idx)
                        if (j != ia && j != ib && j != ic && InTriangle(p[j], a, b, c)) { inside = true; break; }
                    if (inside) continue;
                    result.Add(ia); result.Add(ib); result.Add(ic);
                    idx.RemoveAt(i);
                    clipped = true;
                    break;
                }
                if (!clipped) break; // degenerate outline: keep what we have
            }
            if (idx.Count == 3) { result.Add(idx[0]); result.Add(idx[1]); result.Add(idx[2]); }
            return result;
        }

        static float Cross(Vector2 u, Vector2 v) => u.x * v.y - u.y * v.x;

        static bool InTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = Cross(b - a, p - a), d2 = Cross(c - b, p - b), d3 = Cross(a - c, p - c);
            return d1 > 0f && d2 > 0f && d3 > 0f;
        }

        /// <summary>A regular polygon outline (for round shields, staff heads).</summary>
        public static Vector2[] Circle(Vector2 centre, float radius, int sides = 12)
        {
            var pts = new Vector2[sides];
            for (int i = 0; i < sides; i++)
            {
                float a = i * Mathf.PI * 2f / sides;
                pts[i] = centre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
            }
            return pts;
        }

        public static Vector2[] Rect(float x0, float y0, float x1, float y1) =>
            new[] { new Vector2(x0, y0), new Vector2(x1, y0), new Vector2(x1, y1), new Vector2(x0, y1) };
    }
}
