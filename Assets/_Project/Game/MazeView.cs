using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

namespace MazeRunner
{
    /// <summary>
    /// Renders a <see cref="MazeData"/>. Fixed walls are merged into a few chunk meshes;
    /// every shiftable edge gets its own <see cref="WallSlab"/> that rises out of / sinks into the floor.
    /// </summary>
    public sealed class MazeView : MonoBehaviour
    {
        public const float Cell = 4f;
        public const float WallH = 2.6f;
        public const float Thick = 0.7f;
        public const float PillarT = 0.95f;
        public const float PillarH = WallH + 0.18f;
        const int Chunk = 6;

        public MazeData Maze { get; private set; }
        readonly Dictionary<Edge, WallSlab> _slabs = new();
        readonly List<GameObject> _markers = new();
        ThemeAssets _theme;
        Material _warnClose, _warnOpen;
        Mesh _stripMesh;

        public static Vector3 CellCenter(Vector2Int c) => new((c.x + 0.5f) * Cell, 0f, (c.y + 0.5f) * Cell);

        public static Vector2Int WorldToCell(Vector3 p) => new(Mathf.FloorToInt(p.x / Cell), Mathf.FloorToInt(p.z / Cell));

        public static Vector3 EdgeCenter(Edge e) => e.Vertical
            ? new Vector3(e.I * Cell, 0f, (e.J + 0.5f) * Cell)
            : new Vector3((e.I + 0.5f) * Cell, 0f, e.J * Cell);

        public Vector3 Size => new(Maze.W * Cell, 0, Maze.H * Cell);
        public Vector3 Center => Size * 0.5f;

        public void Build(MazeData maze, ThemeAssets theme)
        {
            Maze = maze;
            _theme = theme;
            var bank = AssetBank.I;
            _warnClose = new Material(bank.Glow); _warnClose.SetColor("_Color", new Color(1f, 0.18f, 0.1f, 1.6f)); _warnClose.SetFloat("_Shape", 1); _warnClose.SetFloat("_Softness", 1.1f);
            _warnOpen = new Material(bank.Glow);  _warnOpen.SetColor("_Color", new Color(0.2f, 0.75f, 1f, 1.4f));  _warnOpen.SetFloat("_Shape", 1); _warnOpen.SetFloat("_Softness", 1.1f);
            _stripMesh = MeshKit.FlatQuad(Cell + 0.6f, 2.6f);

            BuildFloor();
            BuildFixedWalls();
            BuildSlabs();
            maze.WallChanged += OnWallChanged;
        }

        void OnDestroy()
        {
            if (Maze != null) Maze.WallChanged -= OnWallChanged;
            DOTween.Kill(this);
        }

        // ── floor ───────────────────────────────────────────────────────────────
        void BuildFloor()
        {
            for (int cx = 0; cx < Maze.W; cx += Chunk)
            for (int cy = 0; cy < Maze.H; cy += Chunk)
            {
                var kit = new MeshKit { TopUV = 0.25f };
                int x1 = Mathf.Min(cx + Chunk, Maze.W), y1 = Mathf.Min(cy + Chunk, Maze.H);
                kit.Floor(cx * Cell, cy * Cell, x1 * Cell, y1 * Cell, 0f);
                var go = MeshKit.Spawn($"Floor {cx},{cy}", transform, kit.Build("Floor"), _theme.Floor, _theme.Floor);
                go.isStatic = true;
            }

            // Surrounding ground so the camera never sees the void near the border.
            const float m = 40f;
            float w = Maze.W * Cell, h = Maze.H * Cell;
            var ground = new MeshKit { TopUV = 0.2f };
            ground.Floor(-m, -m, w + m, 0, -0.02f);
            ground.Floor(-m, h, w + m, h + m, -0.02f);
            ground.Floor(-m, 0, 0, h, -0.02f);
            ground.Floor(w, 0, w + m, h, -0.02f);
            MeshKit.Spawn("Ground", transform, ground.Build("Ground"), _theme.Ground, _theme.Ground);

            var col = new GameObject("FloorCollider");
            col.transform.SetParent(transform, false);
            var box = col.AddComponent<BoxCollider>();
            box.center = new Vector3(w * 0.5f, -0.5f, h * 0.5f);
            box.size = new Vector3(w + 2 * m, 1f, h + 2 * m);
        }

        // ── fixed walls + pillars ───────────────────────────────────────────────
        void BuildFixedWalls()
        {
            var colliders = new GameObject("WallColliders");
            colliders.transform.SetParent(transform, false);

            for (int cx = 0; cx < Maze.W; cx += Chunk)
            for (int cy = 0; cy < Maze.H; cy += Chunk)
            {
                var kit = new MeshKit();
                int x1 = Mathf.Min(cx + Chunk, Maze.W), y1 = Mathf.Min(cy + Chunk, Maze.H);
                bool lastX = x1 == Maze.W, lastY = y1 == Maze.H;

                for (int i = cx; i <= (lastX ? x1 : x1 - 1); i++)
                for (int j = cy; j <= (lastY ? y1 : y1 - 1); j++)
                {
                    // Pillar at grid vertex (i, j).
                    var p = new Vector3(i * Cell, 0, j * Cell);
                    float pt = PillarT * 0.5f;
                    kit.Box(p + new Vector3(-pt, 0, -pt), p + new Vector3(pt, PillarH, pt));
                    AddCollider(colliders, p + new Vector3(0, PillarH * 0.5f, 0), new Vector3(PillarT, PillarH, PillarT));

                    // Vertical edge (i, j) and horizontal edge (i, j) owned by this vertex.
                    if (j < Maze.H) AddFixedWall(kit, colliders, new Edge(true, i, j));
                    if (i < Maze.W) AddFixedWall(kit, colliders, new Edge(false, i, j));
                }

                if (!kit.Empty)
                    MeshKit.Spawn($"Walls {cx},{cy}", transform, kit.Build("Walls"), _theme.WallSide, _theme.WallTop).isStatic = true;
            }
        }

        void AddFixedWall(MeshKit kit, GameObject colliders, Edge e)
        {
            if (Maze.IsShiftable(e) || !Maze.HasWall(e)) return;
            var (min, max) = WallBounds(e);
            kit.Box(min, max);
            AddCollider(colliders, (min + max) * 0.5f, max - min);
        }

        public static (Vector3 min, Vector3 max) WallBounds(Edge e)
        {
            var c = EdgeCenter(e);
            float half = (Cell - PillarT) * 0.5f, t = Thick * 0.5f;
            var ext = e.Vertical ? new Vector3(t, 0, half) : new Vector3(half, 0, t);
            return (c - ext, c + ext + new Vector3(0, WallH, 0));
        }

        static void AddCollider(GameObject host, Vector3 center, Vector3 size)
        {
            var b = host.AddComponent<BoxCollider>();
            b.center = center;
            b.size = size;
        }

        // ── shifting walls ──────────────────────────────────────────────────────
        void BuildSlabs()
        {
            var root = new GameObject("Slabs").transform;
            root.SetParent(transform, false);
            Mesh vMesh = SlabMesh(true), hMesh = SlabMesh(false);

            Maze.ForEachEdge(e =>
            {
                if (!Maze.IsShiftable(e)) return;
                var go = MeshKit.Spawn($"Slab {e}", root, e.Vertical ? vMesh : hMesh, _theme.WallSide, _theme.WallTop);
                go.transform.localPosition = EdgeCenter(e);
                var slab = go.AddComponent<WallSlab>();
                slab.Init(e, Maze.HasWall(e));
                _slabs[e] = slab;
            });
        }

        static Mesh SlabMesh(bool vertical)
        {
            var (min, max) = WallBounds(new Edge(vertical, 0, 0));
            var c = EdgeCenter(new Edge(vertical, 0, 0));
            var kit = new MeshKit();
            kit.Box(min - c, max - c);
            return kit.Build(vertical ? "SlabV" : "SlabH");
        }

        void OnWallChanged(Edge e, bool on)
        {
            if (_slabs.TryGetValue(e, out var slab)) slab.Set(on);
        }

        // ── shift warnings ──────────────────────────────────────────────────────
        public void ShowWarning(IReadOnlyList<ShiftPlanner.Swap> plan, float duration)
        {
            ClearWarnings();
            foreach (var s in plan)
            {
                SpawnStrip(s.Close, _warnClose, duration);
                SpawnStrip(s.Open, _warnOpen, duration);
                if (_slabs.TryGetValue(s.Open, out var slab)) slab.Tremble(duration);
            }
        }

        void SpawnStrip(Edge e, Material mat, float duration)
        {
            var go = new GameObject("Warn");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = EdgeCenter(e) + Vector3.up * 0.04f;
            go.transform.localRotation = e.Vertical ? Quaternion.Euler(0, 90, 0) : Quaternion.identity;
            go.AddComponent<MeshFilter>().sharedMesh = _stripMesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            go.transform.localScale = new Vector3(1f, 1f, 0.2f);
            go.transform.DOScaleZ(1f, 0.35f).SetEase(Ease.OutBack).SetTarget(this);
            _markers.Add(go);
        }

        public void ClearWarnings()
        {
            foreach (var m in _markers) if (m != null) Destroy(m);
            _markers.Clear();
        }

        void Update()
        {
            if (_markers.Count == 0) return;
            float pulse = 0.8f + 0.2f * Mathf.Sin(Time.time * 14f);
            _warnClose.SetColor("_Color", new Color(1f, 0.2f, 0.1f, 2.8f * pulse));
            _warnOpen.SetColor("_Color", new Color(0.25f, 0.75f, 1f, 2.4f * pulse));
        }
    }
}
