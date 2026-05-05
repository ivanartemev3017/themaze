using System.Collections.Generic;
using UnityEngine;

public class SewerMazeGenerator : MonoBehaviour
{
    [Header("Layout")]
    public int mazeWidth  = 9;
    public int mazeHeight = 9;
    public float cellSize    = 4f;
    public float wallHeight  = 5.5f;
    public float wallThick   = 0.35f;

    [Header("Materials")]
    public Material wallMaterial;
    public Material floorMaterial;
    public Material ceilingMaterial;
    public Material waterMaterial;
    public Material gateMaterial;

    [Header("Gates")]
    [Range(0f, 0.4f)] public float gateDensity = 0.15f;

    public Vector3 StartWorldPosition { get; private set; }
    public Vector3 ExitWorldPosition  { get; private set; }

    // Wall data exposed for enemy BFS
    public bool[,] HWalls { get; private set; } // [x, z] — wall on south side of cell(x,z)
    public bool[,] VWalls { get; private set; } // [x, z] — wall on west  side of cell(x,z)

    GameObject _root;

    // ── Public API ────────────────────────────────────────────────────────────

    public void GenerateMaze()
    {
        if (_root != null) Destroy(_root);
        _root = new GameObject("SewerMaze");
        _root.transform.SetParent(transform);

        InitWalls();
        CarvePassages();
        BuildGeometry();
        PlaceGates();
        PlaceLights();

        StartWorldPosition = CellCenter(0, 0);
        ExitWorldPosition  = CellCenter(mazeWidth - 1, mazeHeight - 1);
    }

    public Vector3 GetRandomCellCenter()
    {
        return CellCenter(Random.Range(0, mazeWidth), Random.Range(0, mazeHeight));
    }

    public bool CanMove(Vector2Int from, Vector2Int to)
    {
        int dx = to.x - from.x, dz = to.y - from.y;
        if (Mathf.Abs(dx) + Mathf.Abs(dz) != 1) return false;

        if (dx ==  1) return !VWalls[from.x + 1, from.y];
        if (dx == -1) return !VWalls[from.x,     from.y];
        if (dz ==  1) return !HWalls[from.x, from.y + 1];
        if (dz == -1) return !HWalls[from.x, from.y    ];
        return false;
    }

    public Vector2Int WorldToCell(Vector3 pos)
    {
        return new Vector2Int(
            Mathf.Clamp(Mathf.FloorToInt(pos.x / cellSize), 0, mazeWidth  - 1),
            Mathf.Clamp(Mathf.FloorToInt(pos.z / cellSize), 0, mazeHeight - 1));
    }

    // ── Generation ────────────────────────────────────────────────────────────

    void InitWalls()
    {
        HWalls = new bool[mazeWidth, mazeHeight + 1];
        VWalls = new bool[mazeWidth + 1, mazeHeight];
        for (int x = 0; x < mazeWidth;     x++)
        for (int z = 0; z <= mazeHeight;    z++) HWalls[x, z] = true;
        for (int x = 0; x <= mazeWidth;     x++)
        for (int z = 0; z < mazeHeight;     z++) VWalls[x, z] = true;
    }

    void CarvePassages()
    {
        var visited = new bool[mazeWidth, mazeHeight];
        var stack   = new Stack<Vector2Int>();
        visited[0, 0] = true;
        stack.Push(Vector2Int.zero);

        while (stack.Count > 0)
        {
            var cur  = stack.Peek();
            var nbrs = UnvisitedNeighbours(cur, visited);
            if (nbrs.Count == 0) { stack.Pop(); continue; }

            var next = nbrs[Random.Range(0, nbrs.Count)];
            int dx = next.x - cur.x, dz = next.y - cur.y;

            if (dx ==  1) VWalls[cur.x + 1, cur.y] = false;
            if (dx == -1) VWalls[cur.x,     cur.y] = false;
            if (dz ==  1) HWalls[cur.x, cur.y + 1] = false;
            if (dz == -1) HWalls[cur.x, cur.y    ] = false;

            visited[next.x, next.y] = true;
            stack.Push(next);
        }
    }

    List<Vector2Int> UnvisitedNeighbours(Vector2Int c, bool[,] visited)
    {
        var list = new List<Vector2Int>(4);
        void Try(int x, int z) { if (x >= 0 && x < mazeWidth && z >= 0 && z < mazeHeight && !visited[x, z]) list.Add(new Vector2Int(x, z)); }
        Try(c.x + 1, c.y); Try(c.x - 1, c.y); Try(c.x, c.y + 1); Try(c.x, c.y - 1);
        return list;
    }

    // ── Geometry ──────────────────────────────────────────────────────────────

    void BuildGeometry()
    {
        var wallMesh  = BoxMesh(new Vector3(1, wallHeight, wallThick));
        var hFlat     = BoxMesh(new Vector3(cellSize + wallThick, 0.25f, cellSize + wallThick));

        for (int x = 0; x < mazeWidth; x++)
        for (int z = 0; z < mazeHeight; z++)
        {
            var origin = new Vector3(x * cellSize, 0, z * cellSize);
            float cx = origin.x + cellSize * 0.5f;
            float cz = origin.z + cellSize * 0.5f;

            // Floor (water surface)
            SpawnMesh(new Vector3(cx, 0.02f, cz), Vector3.one, hFlat, waterMaterial, "Floor");

            // Ceiling
            SpawnMesh(new Vector3(cx, wallHeight + 0.12f, cz), Vector3.one, hFlat, ceilingMaterial, "Ceiling");

            // South wall: HWalls[x, z]
            if (HWalls[x, z])
                SpawnWall(new Vector3(cx, wallHeight * 0.5f, origin.z), new Vector3(cellSize + wallThick, 1, 1), wallMesh);

            // West wall: VWalls[x, z]
            if (VWalls[x, z])
                SpawnWall(new Vector3(origin.x, wallHeight * 0.5f, cz), new Vector3(1, 1, cellSize + wallThick), BoxMesh(new Vector3(wallThick, wallHeight, 1)));

            // North border
            if (z == mazeHeight - 1 && HWalls[x, mazeHeight])
                SpawnWall(new Vector3(cx, wallHeight * 0.5f, origin.z + cellSize), new Vector3(cellSize + wallThick, 1, 1), wallMesh);

            // East border
            if (x == mazeWidth - 1 && VWalls[mazeWidth, z])
                SpawnWall(new Vector3(origin.x + cellSize, wallHeight * 0.5f, cz), new Vector3(1, 1, cellSize + wallThick), BoxMesh(new Vector3(wallThick, wallHeight, 1)));
        }
    }

    void SpawnWall(Vector3 pos, Vector3 scale, Mesh mesh)
    {
        var go = new GameObject("Wall");
        go.transform.SetParent(_root.transform);
        go.transform.position = pos;
        go.transform.localScale = scale;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = wallMaterial;
        var mc = go.AddComponent<MeshCollider>();
        mc.sharedMesh = mesh;
    }

    void SpawnMesh(Vector3 pos, Vector3 scale, Mesh mesh, Material mat, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(_root.transform);
        go.transform.position = pos;
        go.transform.localScale = scale;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        go.AddComponent<MeshCollider>().sharedMesh = mesh;
    }

    // ── Gates ─────────────────────────────────────────────────────────────────

    void PlaceGates()
    {
        if (gateMaterial == null) return;

        var passages = new List<(Vector3 pos, bool isNS)>();

        // Internal horizontal passages (N/S walls that are open)
        for (int x = 0; x < mazeWidth;  x++)
        for (int z = 1; z < mazeHeight;  z++)
            if (!HWalls[x, z])
                passages.Add((new Vector3(x * cellSize + cellSize * 0.5f, 0, z * cellSize), false));

        // Internal vertical passages (E/W walls that are open)
        for (int x = 1; x < mazeWidth;  x++)
        for (int z = 0; z < mazeHeight; z++)
            if (!VWalls[x, z])
                passages.Add((new Vector3(x * cellSize, 0, z * cellSize + cellSize * 0.5f), true));

        Shuffle(passages);
        int count = Mathf.RoundToInt(passages.Count * gateDensity);

        for (int i = 0; i < count; i++)
        {
            var (pos, isNS) = passages[i];
            var rot = isNS ? Quaternion.Euler(0, 90, 0) : Quaternion.identity;
            SpawnGate(pos, rot, Random.Range(0f, 18f));
        }
    }

    void SpawnGate(Vector3 pos, Quaternion rot, float phase)
    {
        var go = new GameObject("SewerGate");
        go.transform.SetParent(_root.transform);
        go.transform.position = pos;
        go.transform.rotation = rot;

        // Visual: dark metal bar grid
        var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
        visual.transform.SetParent(go.transform, false);
        visual.transform.localPosition = new Vector3(0, wallHeight * 0.5f, 0);
        visual.transform.localScale = new Vector3(cellSize - 0.1f, wallHeight, 0.18f);
        visual.GetComponent<MeshRenderer>().sharedMaterial = gateMaterial;
        Object.DestroyImmediate(visual.GetComponent<BoxCollider>());

        // Physics collider on parent
        var col = go.AddComponent<BoxCollider>();
        col.center = new Vector3(0, wallHeight * 0.5f, 0);
        col.size   = new Vector3(cellSize - 0.1f, wallHeight, 0.4f);

        var gate = go.AddComponent<SewerGate>();
        gate.phaseOffset  = phase;
        gate.openHeight   = wallHeight + 0.2f;
        gate.closedY      = 0f;
    }

    // ── Lights ────────────────────────────────────────────────────────────────

    void PlaceLights()
    {
        for (int x = 0; x < mazeWidth;  x++)
        for (int z = 0; z < mazeHeight; z++)
        {
            // Count open passages for this cell
            int p = 0;
            if (x > 0            && !VWalls[x, z])      p++;
            if (x < mazeWidth-1  && !VWalls[x+1, z])    p++;
            if (z > 0            && !HWalls[x, z])      p++;
            if (z < mazeHeight-1 && !HWalls[x, z+1])    p++;

            // Only at junctions (3+) or every 3rd dead-end corridor for atmosphere
            if (p < 3 && Random.value > 0.25f) continue;

            var lg = new GameObject("Light");
            lg.transform.SetParent(_root.transform);
            lg.transform.position = CellCenter(x, z) + new Vector3(0, wallHeight - 0.4f, 0);

            var l = lg.AddComponent<Light>();
            l.type      = LightType.Point;
            l.color     = new Color(0.28f, 0.65f, 0.38f); // sewer green
            l.intensity = 2f;
            l.range     = 9f;
            l.shadows   = LightShadows.None;
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    Vector3 CellCenter(int x, int z) =>
        new Vector3(x * cellSize + cellSize * 0.5f, 0, z * cellSize + cellSize * 0.5f);

    static Mesh BoxMesh(Vector3 s)
    {
        float hx = s.x * 0.5f, hy = s.y * 0.5f, hz = s.z * 0.5f;
        float ux = s.x * 0.25f, uy = s.y * 0.25f, uz = s.z * 0.25f;

        var v = new Vector3[24];
        var u = new Vector2[24];

        // Front -Z
        v[0]=new(-hx,-hy,-hz); v[1]=new(hx,-hy,-hz); v[2]=new(hx,hy,-hz); v[3]=new(-hx,hy,-hz);
        u[0]=new(0,0); u[1]=new(ux,0); u[2]=new(ux,uy); u[3]=new(0,uy);
        // Back +Z
        v[4]=new(hx,-hy,hz); v[5]=new(-hx,-hy,hz); v[6]=new(-hx,hy,hz); v[7]=new(hx,hy,hz);
        u[4]=new(0,0); u[5]=new(ux,0); u[6]=new(ux,uy); u[7]=new(0,uy);
        // Left -X
        v[8]=new(-hx,-hy,hz); v[9]=new(-hx,-hy,-hz); v[10]=new(-hx,hy,-hz); v[11]=new(-hx,hy,hz);
        u[8]=new(0,0); u[9]=new(uz,0); u[10]=new(uz,uy); u[11]=new(0,uy);
        // Right +X
        v[12]=new(hx,-hy,-hz); v[13]=new(hx,-hy,hz); v[14]=new(hx,hy,hz); v[15]=new(hx,hy,-hz);
        u[12]=new(0,0); u[13]=new(uz,0); u[14]=new(uz,uy); u[15]=new(0,uy);
        // Bottom -Y
        v[16]=new(-hx,-hy,hz); v[17]=new(hx,-hy,hz); v[18]=new(hx,-hy,-hz); v[19]=new(-hx,-hy,-hz);
        u[16]=new(0,0); u[17]=new(ux,0); u[18]=new(ux,uz); u[19]=new(0,uz);
        // Top +Y
        v[20]=new(-hx,hy,-hz); v[21]=new(hx,hy,-hz); v[22]=new(hx,hy,hz); v[23]=new(-hx,hy,hz);
        u[20]=new(0,0); u[21]=new(ux,0); u[22]=new(ux,uz); u[23]=new(0,uz);

        var t = new int[36];
        for (int f = 0; f < 6; f++)
        { int b=f*4, i=f*6; t[i]=b; t[i+1]=b+1; t[i+2]=b+2; t[i+3]=b; t[i+4]=b+2; t[i+5]=b+3; }

        var mesh = new Mesh { vertices=v, uv=u, triangles=t };
        mesh.RecalculateNormals();
        return mesh;
    }

    static void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        { int j = Random.Range(0, i + 1); (list[i], list[j]) = (list[j], list[i]); }
    }
}
