using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Generates a 3D maze using the Recursive Backtracker (DFS) algorithm.
/// Each cell is CELL_SIZE units wide. Walls are WALL_HEIGHT units tall.
/// All geometry is parented under a single "Maze" GameObject for easy cleanup.
/// GPU instancing is supported: all walls share wallMaterial, all floors share
/// floorMaterial, all ceilings share ceilingMaterial.
/// </summary>
public class MazeGenerator : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // Public fields — configure in Inspector
    // -------------------------------------------------------------------------

    [Header("Grid Dimensions")]
    public int mazeWidth  = 15;
    public int mazeHeight = 15;

    [Header("Geometry")]
    public float cellSize   = 4f;
    public float wallHeight = 4f;

    [Tooltip("Disable for top-down camera — ceilings block the view from above.")]
    public bool spawnCeilings = false;

    [Header("Shifting Walls")]
    [Range(0f, 0.3f)]
    [Tooltip("Fraction of interior wall segments that become ShiftingWalls (0 = none, 0.15 = 15%).")]
    public float shiftingWallChance = 0.15f;

    [Header("Materials (assign URP Lit materials with Enable GPU Instancing checked)")]
    public Material wallMaterial;
    public Material floorMaterial;
    public Material ceilingMaterial;

    // -------------------------------------------------------------------------
    // Private state
    // -------------------------------------------------------------------------

    // Bitmask flags stored per cell
    private const int NORTH = 1 << 0;
    private const int SOUTH = 1 << 1;
    private const int EAST  = 1 << 2;
    private const int WEST  = 1 << 3;
    private const int VISITED = 1 << 4;

    private int[,] _grid;           // per-cell wall flags
    private GameObject _mazeRoot;   // "Maze" parent in the hierarchy
    private int _debugSpawnCount;   // counts first N spawned objects for diagnostic logging

    // Fired once at the end of GenerateMaze() — subscribe before maze generates.
    // Static so subscribers don't need a MazeGenerator reference.
    public static event System.Action OnMazeReady;

    // Exposed so MazeManager / other systems can read start & exit world positions
    public Vector3 StartWorldPosition { get; private set; }
    public Vector3 ExitWorldPosition  { get; private set; }

    // -------------------------------------------------------------------------
    // Public API — pathfinding support
    // -------------------------------------------------------------------------

    /// <summary>Returns true if cell (x,y) has a passage in the given direction.</summary>
    public bool HasPassage(int x, int y, int direction)
    {
        if (_grid == null || x < 0 || y < 0 || x >= mazeWidth || y >= mazeHeight) return false;
        return (_grid[x, y] & direction) != 0;
    }

    // Direction constants exposed for external use
    public const int DIR_NORTH = 1 << 0;
    public const int DIR_SOUTH = 1 << 1;
    public const int DIR_EAST  = 1 << 2;
    public const int DIR_WEST  = 1 << 3;

    /// <summary>World-space centre of cell (x,y). Returns Vector3.zero if out of bounds.</summary>
    public Vector3 GetCellCentre(int x, int y)
    {
        if (x < 0 || y < 0 || x >= mazeWidth || y >= mazeHeight) return Vector3.zero;
        return CellCentre(x, y);
    }

    /// <summary>Converts world position to grid coordinates.</summary>
    public Vector2Int WorldToCell(Vector3 worldPos)
    {
        int x = Mathf.FloorToInt(worldPos.x / cellSize);
        int y = Mathf.FloorToInt(worldPos.z / cellSize);
        x = Mathf.Clamp(x, 0, mazeWidth  - 1);
        y = Mathf.Clamp(y, 0, mazeHeight - 1);
        return new Vector2Int(x, y);
    }

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    /// <summary>Generates (or regenerates) the full maze.</summary>
    public void GenerateMaze()
    {
        DestroyExistingMaze();
        _mazeRoot = new GameObject("Maze");

        _grid = new int[mazeWidth, mazeHeight];
        CarvePassages(0, 0);
        BuildGeometry();
        SpawnLights();
        SpawnCornerPillars();

        // Start = bottom-left cell centre, Exit = top-right cell centre
        StartWorldPosition = CellCentre(0, 0);
        ExitWorldPosition  = CellCentre(mazeWidth - 1, mazeHeight - 1);

        Debug.Log($"[MazeGenerator] StartWorldPosition = {StartWorldPosition}  " +
                  $"ExitWorldPosition = {ExitWorldPosition}  " +
                  $"(grid {mazeWidth}x{mazeHeight}, cellSize={cellSize}, wallHeight={wallHeight})");

        OnMazeReady?.Invoke();
    }

    /// <summary>Destroys the current maze and generates a fresh one.</summary>
    public void RegenerateMaze()
    {
        GenerateMaze();
    }

    // -------------------------------------------------------------------------
    // Maze algorithm — Recursive Backtracker (iterative to avoid stack overflow
    // on large grids)
    // -------------------------------------------------------------------------

    private void CarvePassages(int startX, int startY)
    {
        var stack = new Stack<Vector2Int>();
        stack.Push(new Vector2Int(startX, startY));
        _grid[startX, startY] |= VISITED;

        while (stack.Count > 0)
        {
            Vector2Int current = stack.Peek();
            List<Vector2Int> neighbours = UnvisitedNeighbours(current.x, current.y);

            if (neighbours.Count == 0)
            {
                stack.Pop();
                continue;
            }

            // Pick a random unvisited neighbour
            Vector2Int next = neighbours[Random.Range(0, neighbours.Count)];

            // Carve the wall between current and next
            RemoveWall(current, next);

            _grid[next.x, next.y] |= VISITED;
            stack.Push(next);
        }
    }

    private List<Vector2Int> UnvisitedNeighbours(int x, int y)
    {
        var list = new List<Vector2Int>(4);
        if (y + 1 < mazeHeight && (_grid[x, y + 1] & VISITED) == 0) list.Add(new Vector2Int(x, y + 1));
        if (y - 1 >= 0         && (_grid[x, y - 1] & VISITED) == 0) list.Add(new Vector2Int(x, y - 1));
        if (x + 1 < mazeWidth  && (_grid[x + 1, y] & VISITED) == 0) list.Add(new Vector2Int(x + 1, y));
        if (x - 1 >= 0         && (_grid[x - 1, y] & VISITED) == 0) list.Add(new Vector2Int(x - 1, y));
        return list;
    }

    private void RemoveWall(Vector2Int a, Vector2Int b)
    {
        int dx = b.x - a.x;
        int dy = b.y - a.y;

        if (dy == 1)       // b is North of a
        {
            _grid[a.x, a.y] |= NORTH;
            _grid[b.x, b.y] |= SOUTH;
        }
        else if (dy == -1) // b is South of a
        {
            _grid[a.x, a.y] |= SOUTH;
            _grid[b.x, b.y] |= NORTH;
        }
        else if (dx == 1)  // b is East of a
        {
            _grid[a.x, a.y] |= EAST;
            _grid[b.x, b.y] |= WEST;
        }
        else if (dx == -1) // b is West of a
        {
            _grid[a.x, a.y] |= WEST;
            _grid[b.x, b.y] |= EAST;
        }
    }

    // -------------------------------------------------------------------------
    // Geometry construction
    // -------------------------------------------------------------------------

    private void BuildGeometry()
    {
        _debugSpawnCount = 0;

        // Shared meshes — reused across all instances (GPU instancing handles
        // per-instance transforms via MaterialPropertyBlock internally)
        Mesh wallMesh  = CreateWallMesh();
        Mesh floorMesh = CreateFloorMesh();

        for (int x = 0; x < mazeWidth; x++)
        {
            for (int y = 0; y < mazeHeight; y++)
            {
                int cell = _grid[x, y];
                Vector3 centre = CellCentre(x, y);

                // Floor tile
                SpawnFloor(centre, floorMesh);

                // Ceiling tile (skip for top-down camera)
                if (spawnCeilings)
                    SpawnCeiling(centre, floorMesh);

                // South wall — border if on the bottom row (y == 0)
                if ((cell & SOUTH) == 0)
                    SpawnWall(centre + new Vector3(0, 0, -cellSize * 0.5f),
                              Quaternion.identity, wallMesh, isBorder: y == 0);

                // West wall — border if on the leftmost column (x == 0)
                if ((cell & WEST) == 0)
                    SpawnWall(centre + new Vector3(-cellSize * 0.5f, 0, 0),
                              Quaternion.Euler(0, 90, 0), wallMesh, isBorder: x == 0);

                // North wall — top border row only, always border
                if (y == mazeHeight - 1 && (cell & NORTH) == 0)
                    SpawnWall(centre + new Vector3(0, 0, cellSize * 0.5f),
                              Quaternion.identity, wallMesh, isBorder: true);

                // East wall — right border column only, always border
                if (x == mazeWidth - 1 && (cell & EAST) == 0)
                    SpawnWall(centre + new Vector3(cellSize * 0.5f, 0, 0),
                              Quaternion.Euler(0, 90, 0), wallMesh, isBorder: true);
            }
        }
    }

    // Wall mesh: 1f thick — matches BoxCollider exactly, no Z-fighting with pillars.
    private Mesh CreateWallMesh()
    {
        return CreateBoxMesh(cellSize, wallHeight, 1f);
    }

    // Floor/ceiling mesh: cellSize × cellSize, 0.2 units thick
    private Mesh CreateFloorMesh()
    {
        return CreateBoxMesh(cellSize, 0.2f, cellSize);
    }

    private Mesh CreateBoxMesh(float w, float h, float d)
    {
        var mesh = new Mesh();
        float hw = w * 0.5f, hh = h * 0.5f, hd = d * 0.5f;

        mesh.vertices = new Vector3[]
        {
            // Front
            new(-hw, -hh,  hd), new( hw, -hh,  hd), new( hw,  hh,  hd), new(-hw,  hh,  hd),
            // Back
            new( hw, -hh, -hd), new(-hw, -hh, -hd), new(-hw,  hh, -hd), new( hw,  hh, -hd),
            // Left
            new(-hw, -hh, -hd), new(-hw, -hh,  hd), new(-hw,  hh,  hd), new(-hw,  hh, -hd),
            // Right
            new( hw, -hh,  hd), new( hw, -hh, -hd), new( hw,  hh, -hd), new( hw,  hh,  hd),
            // Top
            new(-hw,  hh,  hd), new( hw,  hh,  hd), new( hw,  hh, -hd), new(-hw,  hh, -hd),
            // Bottom
            new(-hw, -hh, -hd), new( hw, -hh, -hd), new( hw, -hh,  hd), new(-hw, -hh,  hd),
        };

        mesh.normals = new Vector3[]
        {
            Vector3.forward,  Vector3.forward,  Vector3.forward,  Vector3.forward,
            Vector3.back,     Vector3.back,     Vector3.back,     Vector3.back,
            Vector3.left,     Vector3.left,     Vector3.left,     Vector3.left,
            Vector3.right,    Vector3.right,    Vector3.right,    Vector3.right,
            Vector3.up,       Vector3.up,       Vector3.up,       Vector3.up,
            Vector3.down,     Vector3.down,     Vector3.down,     Vector3.down,
        };

        mesh.uv = new Vector2[]
        {
            new(0,0), new(1,0), new(1,1), new(0,1),
            new(0,0), new(1,0), new(1,1), new(0,1),
            new(0,0), new(1,0), new(1,1), new(0,1),
            new(0,0), new(1,0), new(1,1), new(0,1),
            new(0,0), new(1,0), new(1,1), new(0,1),
            new(0,0), new(1,0), new(1,1), new(0,1),
        };

        mesh.triangles = new int[]
        {
             0, 2, 1,  0, 3, 2,   // Front
             4, 6, 5,  4, 7, 6,   // Back
             8,10, 9,  8,11,10,   // Left
            12,14,13, 12,15,14,   // Right
            16,18,17, 16,19,18,   // Top
            20,22,21, 20,23,22,   // Bottom
        };

        mesh.RecalculateBounds();
        return mesh;
    }

    private void SpawnWall(Vector3 position, Quaternion rotation, Mesh mesh, bool isBorder = false)
    {
        // Walls are centred vertically at half wall height
        position.y = wallHeight * 0.5f;
        var go = new GameObject("Wall");
        if (_debugSpawnCount < 5) { Debug.Log($"[MazeGenerator] spawn #{_debugSpawnCount + 1}: name='{go.name}'  pos={position:F2}  layer={go.layer}"); _debugSpawnCount++; }
        go.transform.SetParent(_mazeRoot.transform, false);
        go.transform.position = position;
        go.transform.rotation = rotation;

        var mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = mesh;

        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = wallMaterial;

        var col = go.AddComponent<BoxCollider>();
        col.size = new Vector3(cellSize, wallHeight, 1f);

        // Eligible interior walls become ShiftingWalls
        if (!isBorder && Random.value < shiftingWallChance)
        {
            go.isStatic = false;
            var rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity  = false;

            var sw = go.AddComponent<ShiftingWall>();
            sw.ShiftDistance = cellSize;
        }
        else
        {
            go.isStatic = true;
        }
    }

    private void SpawnFloor(Vector3 centre, Mesh mesh)
    {
        var go = new GameObject("Floor");
        if (_debugSpawnCount < 5) { Debug.Log($"[MazeGenerator] spawn #{_debugSpawnCount + 1}: name='{go.name}'  pos={(new Vector3(centre.x, -0.1f, centre.z)):F2}  layer={go.layer}"); _debugSpawnCount++; }
        go.transform.SetParent(_mazeRoot.transform, false);
        go.transform.position = new Vector3(centre.x, -0.1f, centre.z);
        go.isStatic = true;

        var mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = mesh;

        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = floorMaterial != null ? floorMaterial : wallMaterial;

        go.AddComponent<BoxCollider>().size = new Vector3(cellSize, 0.2f, cellSize);
    }

    private void SpawnCeiling(Vector3 centre, Mesh mesh)
    {
        var go = new GameObject("Ceiling");
        if (_debugSpawnCount < 5) { Debug.Log($"[MazeGenerator] spawn #{_debugSpawnCount + 1}: name='{go.name}'  pos={(new Vector3(centre.x, wallHeight + 0.1f, centre.z)):F2}  layer={go.layer}"); _debugSpawnCount++; }
        go.transform.SetParent(_mazeRoot.transform, false);
        go.transform.position = new Vector3(centre.x, wallHeight + 0.1f, centre.z);
        go.isStatic = true;

        var mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = mesh;

        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = ceilingMaterial != null ? ceilingMaterial : wallMaterial;

        // No collider on ceiling — player cannot reach it
    }

    // -------------------------------------------------------------------------
    // Lighting
    // -------------------------------------------------------------------------

    private void SpawnLights()
    {
        // Place one Point Light every 2 cells (= cellSize*2 = 8 units) in both
        // axes. Range 9 units (> 2 cells) gives enough overlap that no corridor
        // segment is left dark. Lights sit at half wall-height so they spread
        // evenly to floor and ceiling opening. No shadows — mobile budget.
        const int   step      = 1;                          // EVERY cell gets a light
        float       lightY    = wallHeight * 0.5f;          // 2 units
        float       range     = cellSize   * 3f;             // 12 units
        const float intensity = 7.0f;   // increased — stone texture absorbs more light
        var         color     = new Color(0.55f, 0.65f, 1.0f); // cool moonlight — makes grey walls look like concrete

        for (int x = 0; x < mazeWidth;  x += step)
        for (int y = 0; y < mazeHeight; y += step)
        {
            Vector3 pos  = CellCentre(x, y);
            pos.y = lightY;

            var go = new GameObject("MazeLight");
            go.transform.SetParent(_mazeRoot.transform, false);
            go.transform.position = pos;

            var lt        = go.AddComponent<Light>();
            lt.type       = LightType.Point;
            lt.color      = color;
            lt.intensity  = intensity;
            lt.range      = range;
            lt.shadows    = LightShadows.None;
        }
    }

    // -------------------------------------------------------------------------
    // Corner pillars — fill visual gaps where perpendicular wall ends meet
    // -------------------------------------------------------------------------

    private void SpawnCornerPillars()
    {
        // A 1×wallHeight×1 pillar at every grid vertex (where 4 cells meet).
        // Fills the 0.5-unit gap at wall ends visible from adjacent corridors.
        // GPU instancing via sharedMaterial/sharedMesh batches all pillars together.
        Mesh pillarMesh = CreateBoxMesh(1f, wallHeight, 1f); // matches wall thickness exactly

        for (int gx = 0; gx <= mazeWidth; gx++)
        for (int gy = 0; gy <= mazeHeight; gy++)
        {
            var pos = new Vector3(gx * cellSize, wallHeight * 0.5f, gy * cellSize);
            var go  = new GameObject("Pillar");
            go.transform.SetParent(_mazeRoot.transform, false);
            go.transform.position = pos;
            go.isStatic           = true;

            go.AddComponent<MeshFilter>().sharedMesh       = pillarMesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = wallMaterial;
            go.AddComponent<BoxCollider>();
        }
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    /// <summary>World-space XZ centre of cell (x, y). Y is always 0.</summary>
    private Vector3 CellCentre(int x, int y)
    {
        return new Vector3(x * cellSize + cellSize * 0.5f, 0f, y * cellSize + cellSize * 0.5f);
    }

    private void DestroyExistingMaze()
    {
        if (_mazeRoot != null)
            Destroy(_mazeRoot);

        // Fallback: find by name in case of editor reloads
        var existing = GameObject.Find("Maze");
        if (existing != null)
            Destroy(existing);
    }
}
