using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

/// <summary>
/// Patrol-based maze enemy. Spawns at a random cell far from the player.
/// Two modes:
///   PATROL — BFS to random cells, walks corridors.
///   CHASE  — BFS toward the player, faster speed.
///
/// Detection: periodic distance check (not raycast — walls are procedural boxes
/// and raycasts are unreliable through shifting geometry). If player is within
/// detection range AND in the same corridor (BFS path length ≤ threshold), chase.
///
/// Easy: 0 enemies. Medium: 1 enemy. Hard: 2 enemies.
/// </summary>
public class MazeEnemy : MonoBehaviour
{
    // ---- Configuration (set by spawner) ----
    private float _moveSpeed  = 3.2f;
    private float _chaseSpeed = 4.5f;
    private float _detectionRange = 20f;      // world units — distance check
    private int   _detectionPathLen = 8;       // max BFS path length to trigger chase

    private const float CatchDistance   = 1.6f;
    private const float ReachThreshold  = 0.5f;
    private const float DetectionCheck  = 0.5f;  // seconds between detection checks
    private const float PatrolPauseMin  = 0.5f;
    private const float PatrolPauseMax  = 2.0f;
    private const float DotHeight       = 55f;

    // ---- State ----
    private enum Mode { Patrol, Chase }
    private Mode _mode = Mode.Patrol;

    private Transform      _player;
    private MazeGenerator   _maze;
    private Transform       _enemyTF;
    private List<Vector3>   _path = new();
    private int             _pathIndex;
    private float           _detectionTimer;
    private float           _pauseTimer;
    private float           _loseChaseTimer;   // time since player left detection — return to patrol
    private float           _freezeTimer;      // frozen by artifact
    private float           _speedMultiplier = 1f; // for final rush boost
    private Vector2Int      _lastChaseTarget = new Vector2Int(-1, -1); // last cell we pathed toward
    private Rigidbody       _rb;               // physics body — prevents passing through walls

    // Stuck detection
    private Vector3 _lastStuckCheckPos;
    private float   _stuckTimer;
    private const float StuckCheckInterval = 1.2f;
    private const float StuckMoveThreshold = 0.3f;

    // ---- Animation ----
    private Animation _anim;
    private string    _walkClipName;
    private string    _idleClipName;

    // =========================================================================
    // Public setup — called by EnemySpawner
    // =========================================================================

    public void Setup(Transform player, MazeGenerator maze, float moveSpeed, float chaseSpeed,
                      float detectionRange, int detectionPathLen)
    {
        _player           = player;
        _maze             = maze;
        _moveSpeed        = moveSpeed;
        _chaseSpeed       = chaseSpeed;
        _detectionRange   = detectionRange;
        _detectionPathLen = detectionPathLen;
        _enemyTF          = transform;
        _rb               = GetComponent<Rigidbody>();

        // Cache animation component and find walk/idle clips
        _anim = GetComponentInChildren<Animation>();
        if (_anim != null)
        {
            foreach (AnimationState state in _anim)
            {
                string n = state.name.ToLower();
                if (_walkClipName == null && n.Contains("walk")) _walkClipName = state.name;
                if (_idleClipName == null && n.Contains("idle")) _idleClipName = state.name;
            }
            _anim.wrapMode = WrapMode.Loop;
            string startClip = _walkClipName ?? _idleClipName;
            if (startClip != null) _anim.Play(startClip);
        }

        PickPatrolTarget();
    }

    // =========================================================================
    void Update()
    {
        if (_player == null || _maze == null) return;
        if (GameManager.Instance != null && GameManager.Instance.State != GameManager.GameState.Playing) return;

        // Frozen by artifact
        if (_freezeTimer > 0f)
        {
            _freezeTimer -= Time.deltaTime;
            if (_rb != null) _rb.linearVelocity = Vector3.zero;
            return;
        }

        _detectionTimer += Time.deltaTime;
        if (_detectionTimer >= DetectionCheck)
        {
            _detectionTimer = 0f;
            CheckDetection();
        }

        if (_pauseTimer > 0f)
        {
            _pauseTimer -= Time.deltaTime;
            if (_rb != null) _rb.linearVelocity = Vector3.zero;
            UpdateAnimationSpeed(0f);
            return;
        }

        MoveAlongPath();
        CheckCatch();
        CheckStuck();
        UpdateAnimationSpeed((_mode == Mode.Chase ? _chaseSpeed : _moveSpeed) * _speedMultiplier);
    }

    /// <summary>Freeze enemy for duration (artifact effect).</summary>
    public void Freeze(float duration) => _freezeTimer = duration;

    /// <summary>Multiply speed (for final rush).</summary>
    public void SetSpeedMultiplier(float mult) => _speedMultiplier = mult;

    // =========================================================================
    // Detection
    // =========================================================================

    private void CheckDetection()
    {
        float dist = Vector3.Distance(_enemyTF.position, _player.position);

        if (dist < _detectionRange)
        {
            // Check if player is reachable within short BFS path (same corridor area)
            var enemyCell  = _maze.WorldToCell(_enemyTF.position);
            var playerCell = _maze.WorldToCell(_player.position);
            var path = BFSPath(enemyCell, playerCell, _detectionPathLen + 2);

            if (path != null && path.Count <= _detectionPathLen)
            {
                bool justEnteredChase = _mode != Mode.Chase;
                if (justEnteredChase)
                {
                    _mode = Mode.Chase;
                    Debug.Log("[MazeEnemy] Chase mode!");
                }
                _loseChaseTimer = 0f;
                // Only repath if player moved to a new cell — prevents oscillation when player stands still
                if (justEnteredChase || playerCell != _lastChaseTarget)
                {
                    _lastChaseTarget = playerCell;
                    SetPathFromCells(path);
                }
                return;
            }
        }

        if (_mode == Mode.Chase)
        {
            _loseChaseTimer += DetectionCheck;
            if (_loseChaseTimer > 4f)
            {
                _mode = Mode.Patrol;
                _loseChaseTimer = 0f;
                PickPatrolTarget();
                Debug.Log("[MazeEnemy] Lost player, back to patrol.");
            }
        }
    }

    // =========================================================================
    // Movement
    // =========================================================================

    private void MoveAlongPath()
    {
        if (_path.Count == 0 || _pathIndex >= _path.Count)
        {
            if (_rb != null) _rb.linearVelocity = Vector3.zero;
            if (_mode == Mode.Chase)
            {
                // Re-path to player
                var enemyCell  = _maze.WorldToCell(_enemyTF.position);
                var playerCell = _maze.WorldToCell(_player.position);
                var path = BFSPath(enemyCell, playerCell, 100);
                if (path != null) SetPathFromCells(path);
                else { _mode = Mode.Patrol; PickPatrolTarget(); }
            }
            else
            {
                _pauseTimer = Random.Range(PatrolPauseMin, PatrolPauseMax);
                PickPatrolTarget();
            }
            return;
        }

        float speed = (_mode == Mode.Chase ? _chaseSpeed : _moveSpeed) * _speedMultiplier;
        Vector3 target = _path[_pathIndex];

        // Only aim at the player's exact position when in the same cell —
        // otherwise keep BFS waypoints so the enemy doesn't cut through walls.
        if (_mode == Mode.Chase && _player != null)
        {
            var ec = _maze.WorldToCell(_enemyTF.position);
            var pc = _maze.WorldToCell(_player.position);
            if (ec == pc)
                target = _player.position;
        }

        target.y = _enemyTF.position.y;  // keep on ground

        float dist = Vector3.Distance(_enemyTF.position, target);
        if (dist <= ReachThreshold)
        {
            _pathIndex++;
            return;
        }

        Vector3 dir = (target - _enemyTF.position).normalized;

        // Velocity-based movement so the Rigidbody+CapsuleCollider can block wall geometry.
        // Falls back to direct position set if no Rigidbody (shouldn't happen after SpawnEnemy).
        if (_rb != null)
            _rb.linearVelocity = new Vector3(dir.x * speed, 0f, dir.z * speed);
        else
            _enemyTF.position = Vector3.MoveTowards(_enemyTF.position, target, speed * Time.deltaTime);

        if (dir.sqrMagnitude > 0.001f)
            _enemyTF.rotation = Quaternion.Slerp(
                _enemyTF.rotation, Quaternion.LookRotation(dir), 8f * Time.deltaTime);
    }

    // =========================================================================
    // Patrol
    // =========================================================================

    private void PickPatrolTarget()
    {
        // Pick a random cell and BFS to it
        var start  = _maze.WorldToCell(_enemyTF.position);
        var target = new Vector2Int(
            Random.Range(0, _maze.mazeWidth),
            Random.Range(0, _maze.mazeHeight));

        var path = BFSPath(start, target, 200);
        if (path != null && path.Count > 1)
            SetPathFromCells(path);
    }

    private void SetPathFromCells(List<Vector2Int> cells)
    {
        _path.Clear();
        _pathIndex = 0;
        foreach (var c in cells)
            _path.Add(_maze.GetCellCentre(c.x, c.y));
    }

    // =========================================================================
    // BFS pathfinding through maze grid
    // =========================================================================

    private List<Vector2Int> BFSPath(Vector2Int from, Vector2Int to, int maxSteps)
    {
        if (from == to) return new List<Vector2Int> { from };

        var visited = new HashSet<Vector2Int>();
        var parent  = new Dictionary<Vector2Int, Vector2Int>();
        var queue   = new Queue<Vector2Int>();

        queue.Enqueue(from);
        visited.Add(from);

        int steps = 0;
        while (queue.Count > 0 && steps < maxSteps * 4)
        {
            steps++;
            var current = queue.Dequeue();
            if (current == to)
                return ReconstructPath(parent, from, to);

            // Check all 4 directions
            TryEnqueue(current, 0,  1, MazeGenerator.DIR_NORTH, visited, parent, queue);
            TryEnqueue(current, 0, -1, MazeGenerator.DIR_SOUTH, visited, parent, queue);
            TryEnqueue(current, 1,  0, MazeGenerator.DIR_EAST,  visited, parent, queue);
            TryEnqueue(current,-1,  0, MazeGenerator.DIR_WEST,  visited, parent, queue);
        }

        return null; // no path found within max steps
    }

    private void TryEnqueue(Vector2Int current, int dx, int dy, int dir,
                            HashSet<Vector2Int> visited,
                            Dictionary<Vector2Int, Vector2Int> parent,
                            Queue<Vector2Int> queue)
    {
        if (!_maze.HasPassage(current.x, current.y, dir)) return;
        var next = new Vector2Int(current.x + dx, current.y + dy);
        if (visited.Contains(next)) return;
        visited.Add(next);
        parent[next] = current;
        queue.Enqueue(next);
    }

    private static List<Vector2Int> ReconstructPath(
        Dictionary<Vector2Int, Vector2Int> parent, Vector2Int from, Vector2Int to)
    {
        var path = new List<Vector2Int>();
        var current = to;
        while (current != from)
        {
            path.Add(current);
            current = parent[current];
        }
        path.Add(from);
        path.Reverse();
        return path;
    }

    // =========================================================================
    // Stuck detection — force re-path if the enemy barely moved
    // =========================================================================

    private void CheckStuck()
    {
        _stuckTimer += Time.deltaTime;
        if (_stuckTimer < StuckCheckInterval) return;

        float moved = Vector3.Distance(_enemyTF.position, _lastStuckCheckPos);
        _lastStuckCheckPos = _enemyTF.position;
        _stuckTimer = 0f;

        if (moved < StuckMoveThreshold && _path.Count > 0)
        {
            _lastChaseTarget = new Vector2Int(-1, -1);
            if (_mode == Mode.Chase)
            {
                var enemyCell  = _maze.WorldToCell(_enemyTF.position);
                var playerCell = _maze.WorldToCell(_player.position);
                var path = BFSPath(enemyCell, playerCell, 100);
                if (path != null) SetPathFromCells(path);
                else { _mode = Mode.Patrol; PickPatrolTarget(); }
            }
            else
            {
                PickPatrolTarget();
            }
        }
    }

    // =========================================================================
    // Catch
    // =========================================================================

    private void CheckCatch()
    {
        if (Vector3.Distance(_enemyTF.position, _player.position) < CatchDistance)
            GameManager.Instance?.Lose("CAUGHT", "A creature got you.");
    }

    // =========================================================================
    // Animation
    // =========================================================================

    private void UpdateAnimationSpeed(float speed)
    {
        if (_anim == null || _walkClipName == null) return;
        if (!_anim.IsPlaying(_walkClipName)) _anim.Play(_walkClipName);
        // Normalise to the base walk speed so the feet match movement
        _anim[_walkClipName].speed = (speed > 0.01f) ? speed / _moveSpeed : 0.5f;
    }

    // =========================================================================
    // Spawn helper (static — called by EnemySpawner)
    // =========================================================================

    public static GameObject SpawnEnemy(Vector3 position)
    {
        string biomeEnemyPath = BiomeLibrary.Current?.enemyPrefabPath;
        return string.IsNullOrEmpty(biomeEnemyPath)
            ? SpawnDementor(position)
            : SpawnSpider(position, biomeEnemyPath);
    }

    // ── Desert spider enemy ───────────────────────────────────────────────────
    private static GameObject SpawnSpider(Vector3 position, string prefabPath)
    {
        var go = new GameObject("MazeEnemy");
        go.transform.position = position;

        var tempPrim       = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        var fallbackShader = tempPrim.GetComponent<MeshRenderer>().sharedMaterial.shader;
        Object.Destroy(tempPrim);
        // Lit first — always present in URP builds, same as SpawnDementor.
        var shader = Shader.Find("Universal Render Pipeline/Lit")
                  ?? Shader.Find("Universal Render Pipeline/Unlit")
                  ?? fallbackShader;

        var prefab = Resources.Load<GameObject>(prefabPath);
        if (prefab != null)
        {
            var model = Object.Instantiate(prefab, go.transform);
            model.name = "SpiderModel";
            model.transform.localPosition = new Vector3(0f, 0f, 0f);
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale    = new Vector3(0.6f, 0.6f, 0.6f);

            foreach (var col in model.GetComponentsInChildren<Collider>())
                Object.Destroy(col);

            // Pitch black Unlit — must set _BaseColor, not legacy _Color (URP ignores _Color)
            var spiderMat = new Material(shader);
            spiderMat.SetFloat("_Surface", 0f);              // 0 = Opaque
            spiderMat.SetColor("_BaseColor", Color.black);
            spiderMat.color = Color.black;
            foreach (var r in model.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                var mats = new Material[r.sharedMaterials.Length];
                for (int i = 0; i < mats.Length; i++) mats[i] = spiderMat;
                r.sharedMaterials = mats;
            }
            foreach (var r in model.GetComponentsInChildren<MeshRenderer>())
            {
                var mats = new Material[r.sharedMaterials.Length];
                for (int i = 0; i < mats.Length; i++) mats[i] = spiderMat;
                r.sharedMaterials = mats;
            }

            // Add Legacy Animation and play Walk cycle
            var anim     = model.AddComponent<Animation>();
            var walkClip = Resources.Load<AnimationClip>("Spiders/SpiderWalk");
            var idleClip = Resources.Load<AnimationClip>("Spiders/SpiderIdle");
            if (walkClip != null)
            {
                anim.AddClip(walkClip, "Walk");
                anim.wrapMode = WrapMode.Loop;
                anim.Play("Walk");
            }
            if (idleClip != null)
                anim.AddClip(idleClip, "Idle");
        }
        else
        {
            Debug.LogWarning($"[MazeEnemy] Spider prefab not found at Resources/{prefabPath}");
            BuildProceduralDementor(go, shader);
        }

        // Red spider eyes (low to ground — spider body height ~0.4m)
        SpawnEye(go, new Vector3(-0.08f, 0.35f, 0.18f), new Color(1.0f, 0.15f, 0.0f), 8f, 6f);
        SpawnEye(go, new Vector3( 0.08f, 0.35f, 0.18f), new Color(1.0f, 0.15f, 0.0f), 8f, 6f);

        // Heat aura — orange/red (desert)
        var auraGO = new GameObject("HeatAura");
        auraGO.transform.SetParent(go.transform, false);
        var aura       = auraGO.AddComponent<Light>();
        aura.type      = LightType.Point;
        aura.color     = new Color(1.0f, 0.45f, 0.1f);
        aura.intensity = 2.5f;
        aura.range     = 9f;
        aura.shadows   = LightShadows.None;

        SpawnMinimapDot(go);

        // Physics — spider is low and wide
        var rb = go.AddComponent<Rigidbody>();
        rb.isKinematic   = false;
        rb.useGravity    = false;
        rb.linearDamping = 5f;
        rb.angularDamping = 999f;
        rb.constraints   = RigidbodyConstraints.FreezeRotation
                         | RigidbodyConstraints.FreezePositionY;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

        var cap    = go.AddComponent<CapsuleCollider>();
        cap.radius = 0.35f;
        cap.height = 0.8f;
        cap.center = new Vector3(0f, 0.4f, 0f);

        return go;
    }

    // ── Dungeon dementor enemy ────────────────────────────────────────────────
    private static GameObject SpawnDementor(Vector3 position)
    {
        var go = new GameObject("MazeEnemy");
        go.transform.position = position;

        var tempPrim      = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        var fallbackShader = tempPrim.GetComponent<MeshRenderer>().sharedMaterial.shader;
        Object.Destroy(tempPrim);
        var shader = Shader.Find("Universal Render Pipeline/Lit")
                  ?? Shader.Find("Universal Render Pipeline/Unlit")
                  ?? Shader.Find("Unlit/Color")
                  ?? fallbackShader;

        var prefab = Resources.Load<GameObject>("Creep");
        if (prefab != null)
        {
            var model = Object.Instantiate(prefab, go.transform);
            model.name = "CreepModel";
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale    = new Vector3(0.5f, 0.5f, 0.5f);

            foreach (var col in model.GetComponentsInChildren<Collider>())
                Object.Destroy(col);

            var darkMat   = new Material(shader);
            darkMat.color = new Color(0.06f, 0.04f, 0.10f);
            foreach (var r in model.GetComponentsInChildren<SkinnedMeshRenderer>())
                r.sharedMaterial = darkMat;
            foreach (var r in model.GetComponentsInChildren<MeshRenderer>())
                r.sharedMaterial = darkMat;
        }
        else
        {
            Debug.LogWarning("[MazeEnemy] Creep.fbx not found in Resources — using procedural fallback.");
            BuildProceduralDementor(go, shader);
        }

        SpawnEye(go, new Vector3(-0.04f, 0.95f, 0.12f), new Color(0.55f, 0.88f, 1.0f), 10f, 8f);
        SpawnEye(go, new Vector3( 0.04f, 0.95f, 0.12f), new Color(0.55f, 0.88f, 1.0f), 10f, 8f);

        var auraGO = new GameObject("ColdAura");
        auraGO.transform.SetParent(go.transform, false);
        var aura       = auraGO.AddComponent<Light>();
        aura.type      = LightType.Point;
        aura.color     = new Color(0.30f, 0.42f, 1.0f);
        aura.intensity = 3.5f;
        aura.range     = 11f;
        aura.shadows   = LightShadows.None;

        SpawnMinimapDot(go);

        var rb           = go.AddComponent<Rigidbody>();
        rb.isKinematic   = false;
        rb.useGravity    = false;
        rb.linearDamping          = 5f;
        rb.angularDamping   = 999f;
        rb.constraints   = RigidbodyConstraints.FreezeRotation
                         | RigidbodyConstraints.FreezePositionY;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

        var cap    = go.AddComponent<CapsuleCollider>();
        cap.radius = 0.28f;
        cap.height = 1.8f;
        cap.center = new Vector3(0f, 0.9f, 0f);

        return go;
    }

    // ── Procedural Dementor — used when Creep.fbx not yet imported ───────────
    private static void BuildProceduralDementor(GameObject go, UnityEngine.Shader shader)
    {
        Color darkRobe = new Color(0.04f, 0.02f, 0.07f);
        Color midRobe  = new Color(0.06f, 0.03f, 0.11f);
        Material MakeMat(Color c) { var m = new Material(shader); m.color = c; return m; }

        var vis = new GameObject("Visuals");
        vis.transform.SetParent(go.transform, false);
        vis.transform.DOLocalMoveY(0.22f, 2.1f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo);

        var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.transform.SetParent(vis.transform, false);
        body.transform.localPosition = new Vector3(0f, 1.7f, 0f);
        body.transform.localScale    = new Vector3(0.40f, 1.40f, 0.40f);
        Object.Destroy(body.GetComponent<Collider>());
        body.GetComponent<MeshRenderer>().sharedMaterial = MakeMat(darkRobe);

        var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        head.transform.SetParent(vis.transform, false);
        head.transform.localPosition = new Vector3(0f, 3.35f, 0f);
        head.transform.localScale    = new Vector3(0.46f, 0.54f, 0.46f);
        Object.Destroy(head.GetComponent<Collider>());
        head.GetComponent<MeshRenderer>().sharedMaterial = MakeMat(darkRobe);

        var hood = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        hood.transform.SetParent(vis.transform, false);
        hood.transform.localPosition    = new Vector3(0f, 3.6f, 0.05f);
        hood.transform.localScale       = new Vector3(0.64f, 0.64f, 0.64f);
        hood.transform.localEulerAngles = new Vector3(18f, 0f, 0f);
        Object.Destroy(hood.GetComponent<Collider>());
        hood.GetComponent<MeshRenderer>().sharedMaterial = MakeMat(midRobe);

        float[] tendrilAngles = { 0f, 51f, 103f, 154f, 205f, 257f, 308f };
        float[] tendrilBias   = { 0f, 0.12f, 0.07f, 0.15f, 0.04f, 0.10f, 0.08f };
        for (int i = 0; i < tendrilAngles.Length; i++)
        {
            float rad     = tendrilAngles[i] * Mathf.Deg2Rad;
            var tendril   = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            tendril.transform.SetParent(vis.transform, false);
            float tx = Mathf.Sin(rad) * 0.21f;
            float tz = Mathf.Cos(rad) * 0.21f;
            float baseY = 0.48f + tendrilBias[i];
            tendril.transform.localPosition    = new Vector3(tx, baseY, tz);
            tendril.transform.localScale       = new Vector3(0.09f, 0.52f, 0.09f);
            tendril.transform.localEulerAngles = new Vector3(-Mathf.Cos(rad)*14f, 0f, Mathf.Sin(rad)*14f);
            Object.Destroy(tendril.GetComponent<Collider>());
            tendril.GetComponent<MeshRenderer>().sharedMaterial = MakeMat(darkRobe);
            float swayAmt = Random.Range(0.09f, 0.22f);
            float swayDur = Random.Range(0.65f, 1.55f);
            tendril.transform.DOLocalMoveY(baseY + swayAmt, swayDur)
                   .SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo);
        }
    }

    // Minimap dot — sphere at Y=DotHeight, emissive red via URP Lit.
    // Emission is visible regardless of scene lighting (no Unlit dependency).
    // Shadow casting disabled: the sphere was casting a false shadow on the ground.
    private static void SpawnMinimapDot(GameObject parent)
    {
        var dot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        dot.name = "EnemyMinimapDot";
        dot.transform.SetParent(parent.transform, false);
        dot.transform.localPosition = Vector3.up * DotHeight;
        dot.transform.localScale    = Vector3.one * 3.0f;
        Object.Destroy(dot.GetComponent<Collider>());

        var mr = dot.GetComponent<MeshRenderer>();
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows    = false;

        // URP Lit is always included in URP builds — reliable on Android.
        // Emission makes the dot visible on the minimap regardless of ambient lighting.
        var litShader = Shader.Find("Universal Render Pipeline/Lit");
        if (litShader != null)
        {
            var dotMat = new Material(litShader);
            dotMat.SetColor("_BaseColor",    Color.black);
            dotMat.SetColor("_EmissionColor", Color.red * 4f);
            dotMat.EnableKeyword("_EMISSION");
            mr.sharedMaterial = dotMat;
        }
    }

    private static void SpawnEye(GameObject parent, Vector3 localPos,
                                  Color color, float intensity, float range)
    {
        var go = new GameObject("Eye");
        go.transform.SetParent(parent.transform, false);
        go.transform.localPosition = localPos;
        var lt       = go.AddComponent<Light>();
        lt.type      = LightType.Point;
        lt.color     = color;
        lt.intensity = intensity;
        lt.range     = range;
        lt.shadows   = LightShadows.None;
    }
}
