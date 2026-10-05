using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MazeRunner
{
    /// <summary>Accumulates boxes and quads into one mesh with two submeshes: 0 = sides, 1 = tops.</summary>
    public sealed class MeshKit
    {
        readonly List<Vector3> _v = new();
        readonly List<Vector3> _n = new();
        readonly List<Vector2> _uv = new();
        readonly List<int> _side = new();
        readonly List<int> _top = new();

        public float SideUV = 0.4f;  // texture repeats per metre on walls
        public float TopUV = 0.33f;
        public bool WorldUV = true;

        public bool Empty => _v.Count == 0;

        /// <summary>Axis-aligned box from <paramref name="min"/> to <paramref name="max"/>; bottom face omitted.</summary>
        public void Box(Vector3 min, Vector3 max)
        {
            var a = min; var b = max;
            // +X, -X, +Z, -Z
            Quad(new(b.x, a.y, a.z), new(b.x, a.y, b.z), new(b.x, b.y, b.z), new(b.x, b.y, a.z), Vector3.right, false);
            Quad(new(a.x, a.y, b.z), new(a.x, a.y, a.z), new(a.x, b.y, a.z), new(a.x, b.y, b.z), Vector3.left, false);
            Quad(new(b.x, a.y, b.z), new(a.x, a.y, b.z), new(a.x, b.y, b.z), new(b.x, b.y, b.z), Vector3.forward, false);
            Quad(new(a.x, a.y, a.z), new(b.x, a.y, a.z), new(b.x, b.y, a.z), new(a.x, b.y, a.z), Vector3.back, false);
            // top
            Quad(new(a.x, b.y, a.z), new(b.x, b.y, a.z), new(b.x, b.y, b.z), new(a.x, b.y, b.z), Vector3.up, true);
        }

        /// <summary>Upward-facing rectangle at height y (floor pieces).</summary>
        public void Floor(float x0, float z0, float x1, float z1, float y)
        {
            Quad(new(x0, y, z0), new(x1, y, z0), new(x1, y, z1), new(x0, y, z1), Vector3.up, true);
        }

        void Quad(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Vector3 n, bool top)
        {
            int i = _v.Count;
            _v.Add(p0); _v.Add(p1); _v.Add(p2); _v.Add(p3);
            _n.Add(n); _n.Add(n); _n.Add(n); _n.Add(n);
            _uv.Add(UV(p0, n, top)); _uv.Add(UV(p1, n, top)); _uv.Add(UV(p2, n, top)); _uv.Add(UV(p3, n, top));
            var list = top ? _top : _side;
            // Clockwise winding when viewed from the normal side (Unity front faces).
            list.Add(i); list.Add(i + 2); list.Add(i + 1);
            list.Add(i); list.Add(i + 3); list.Add(i + 2);
        }

        Vector2 UV(Vector3 p, Vector3 n, bool top)
        {
            if (top) return new Vector2(p.x, p.z) * TopUV;
            float u = Mathf.Abs(n.x) > 0.5f ? p.z : p.x;
            return new Vector2(u, p.y) * SideUV;
        }

        public Mesh Build(string name)
        {
            var m = new Mesh { name = name };
            if (_v.Count > 65000) m.indexFormat = IndexFormat.UInt32;
            m.SetVertices(_v);
            m.SetNormals(_n);
            m.SetUVs(0, _uv);
            m.subMeshCount = 2;
            m.SetTriangles(_side, 0);
            m.SetTriangles(_top, 1);
            m.RecalculateBounds();
            m.RecalculateTangents();
            m.UploadMeshData(true);
            return m;
        }

        public static GameObject Spawn(string name, Transform parent, Mesh mesh, Material side, Material top)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterials = new[] { side, top };
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = LightProbeUsage.Off;
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return go;
        }

        /// <summary>Flat quad in the XZ plane centred on the origin, UV 0..1. Used for glows and markers.</summary>
        public static Mesh FlatQuad(float sx, float sz)
        {
            var m = new Mesh { name = "FlatQuad" };
            float x = sx * 0.5f, z = sz * 0.5f;
            m.vertices = new[] { new Vector3(-x, 0, -z), new Vector3(x, 0, -z), new Vector3(x, 0, z), new Vector3(-x, 0, z) };
            m.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
            m.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        /// <summary>Vertical quad (XY plane) with its bottom edge at the origin, UV 0..1 — light beams.</summary>
        public static Mesh UprightQuad(float width, float height)
        {
            var m = new Mesh { name = "UprightQuad" };
            float x = width * 0.5f;
            m.vertices = new[] { new Vector3(-x, 0, 0), new Vector3(x, 0, 0), new Vector3(x, height, 0), new Vector3(-x, height, 0) };
            m.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
            m.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        /// <summary>Low-poly double pyramid — shards and keys.</summary>
        public static Mesh Gem(float radius, float height, int sides = 6)
        {
            var v = new List<Vector3>(); var t = new List<int>();
            var top = new Vector3(0, height * 0.5f, 0); var bot = new Vector3(0, -height * 0.5f, 0);
            for (int i = 0; i < sides; i++)
            {
                float a0 = i * Mathf.PI * 2f / sides, a1 = (i + 1) * Mathf.PI * 2f / sides;
                var p0 = new Vector3(Mathf.Cos(a0) * radius, 0, Mathf.Sin(a0) * radius);
                var p1 = new Vector3(Mathf.Cos(a1) * radius, 0, Mathf.Sin(a1) * radius);
                int b = v.Count;
                v.Add(top); v.Add(p1); v.Add(p0);
                v.Add(bot); v.Add(p0); v.Add(p1);
                t.Add(b); t.Add(b + 1); t.Add(b + 2);
                t.Add(b + 3); t.Add(b + 4); t.Add(b + 5);
            }
            var m = new Mesh { name = "Gem" };
            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }
    }
}
