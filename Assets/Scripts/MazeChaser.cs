using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;

/// <summary>
/// Trail-following pursuer. Spawns on Medium and Hard only.
///
/// Records the player's exact path. After a difficulty-based delay the enemy
/// starts walking the same path — it can never clip through walls because it
/// retraces positions the player actually walked.
///
/// If a wall shifts and blocks the recorded trail the enemy's movement slows
/// naturally. Stuck-detection skips a few trail points so the enemy recovers
/// and resumes the chase.
///
/// Catch distance → instant Lose("CAUGHT", …).
/// </summary>
public class MazeChaser : MonoBehaviour
{
    // ---- Trail recording ----
    private const float RecordInterval  = 0.3f;   // seconds between trail snapshots
    private const int   MaxTrailPoints  = 2000;   // safety cap

    // ---- Movement ----
    private const float ReachThreshold  = 0.35f;  // units — considered "at" a trail point
    private const float CatchDistance   = 1.6f;   // units — triggers lose

    // ---- Stuck detection ----
    private const float StuckCheckTime  = 2.0f;   // seconds between stuck checks
    private const float StuckMinMove    = 0.25f;  // must move this far or deemed stuck
    private const int   StuckSkipCount  = 10;     // trail points to skip when stuck

    // ---- Minimap dot height ----
    private const float DotHeight       = 55f;    // above maze geometry, invisible to main cam

    // ==========================================================================
    private Transform          _player;
    private readonly Queue<Vector3> _trail = new();

    private float   _recordTimer;
    private float   _activeTimer;
    private bool    _isActive;
    private Vector3? _currentTarget;

    // Stuck detection
    private float   _stuckTimer;
    private Vector3 _lastStuckCheckPos;

    // Per-difficulty settings
    private float   _delay;   // seconds before enemy starts moving
    private float   _speed;

    // The enemy GameObject in the scene
    private Transform _enemyTF;

    // ==========================================================================
    void OnEnable()  => PlayerSpawner.OnPlayerSpawned += OnPlayerSpawned;
    void OnDisable() => PlayerSpawner.OnPlayerSpawned -= OnPlayerSpawned;

    // ==========================================================================
    private void OnPlayerSpawned(GameObject player)
    {
        // DISABLED — replaced by MazeEnemy + EnemySpawner (patrol-based enemies)
        return;
    }

    // ==========================================================================
    void Update()
    {
        if (_player == null || _enemyTF == null) return;
        if (GameManager.Instance != null && GameManager.Instance.State != GameManager.GameState.Playing) return;

        RecordTrail();

        if (!_isActive)
        {
            _activeTimer += Time.deltaTime;
            if (_activeTimer >= _delay && _trail.Count > 5)
            {
                _isActive = true;
                _lastStuckCheckPos = _enemyTF.position;
                Debug.Log("[MazeChaser] Pursuer activated.");
            }
            return;
        }

        MoveAlongTrail();
        CheckCatch();
        CheckStuck();
    }

    // ==========================================================================
    // Trail
    // ==========================================================================

    private void RecordTrail()
    {
        _recordTimer += Time.deltaTime;
        if (_recordTimer < RecordInterval) return;
        _recordTimer = 0f;

        if (_trail.Count < MaxTrailPoints)
            _trail.Enqueue(_player.position);
    }

    // ==========================================================================
    // Movement
    // ==========================================================================

    private void MoveAlongTrail()
    {
        // Grab next target if needed
        if (_currentTarget == null)
        {
            if (_trail.Count == 0) return;
            _currentTarget = _trail.Dequeue();
        }

        Vector3 target = _currentTarget.Value;
        Vector3 pos    = _enemyTF.position;
        float   dist   = Vector3.Distance(pos, target);

        if (dist <= ReachThreshold)
        {
            _currentTarget = null;   // reached — pull next point next frame
            return;
        }

        // Move toward target (no physics — trail points are always walkable)
        Vector3 dir  = (target - pos).normalized;
        dir.y        = 0f;  // keep on ground plane
        _enemyTF.position = Vector3.MoveTowards(pos, pos + dir, _speed * Time.deltaTime);

        // Face movement direction
        if (dir.sqrMagnitude > 0.001f)
            _enemyTF.rotation = Quaternion.Slerp(
                _enemyTF.rotation, Quaternion.LookRotation(dir), 8f * Time.deltaTime);
    }

    // ==========================================================================
    // Stuck detection
    // ==========================================================================

    private void CheckStuck()
    {
        _stuckTimer += Time.deltaTime;
        if (_stuckTimer < StuckCheckTime) return;
        _stuckTimer = 0f;

        float moved = Vector3.Distance(_enemyTF.position, _lastStuckCheckPos);
        if (moved < StuckMinMove)
        {
            // Skip ahead in the trail — wall shifted, find clear path
            int skip = Mathf.Min(StuckSkipCount, _trail.Count);
            for (int i = 0; i < skip; i++)
                if (_trail.Count > 0) _trail.Dequeue();
            _currentTarget = null;
            Debug.Log($"[MazeChaser] Stuck — skipped {skip} trail points.");
        }

        _lastStuckCheckPos = _enemyTF.position;
    }

    // ==========================================================================
    // Catch
    // ==========================================================================

    private void CheckCatch()
    {
        if (Vector3.Distance(_enemyTF.position, _player.position) < CatchDistance)
            GameManager.Instance?.Lose("CAUGHT", "The creature got you.");
    }

    // ==========================================================================
    // Spawn
    // ==========================================================================

    private void SpawnEnemy()
    {
        var mgr = FindAnyObjectByType<MazeManager>();
        Vector3 spawnPos = mgr != null ? mgr.StartPosition : Vector3.zero;
        spawnPos.y = 0.05f;

        var go = new GameObject("MazeChaser");
        go.transform.position = spawnPos;
        _enemyTF = go.transform;

        BuildVisual(go);
        BuildMinimapDot(go);
    }

    private static void BuildVisual(GameObject parent)
    {
        // Dark body
        var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.transform.SetParent(parent.transform, false);
        body.transform.localPosition = new Vector3(0f, 1f, 0f);
        body.transform.localScale    = new Vector3(0.75f, 1.0f, 0.75f);
        Object.Destroy(body.GetComponent<Collider>());

        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = new Color(0.04f, 0.01f, 0.01f);
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", new Color(0.15f, 0f, 0f));
        body.GetComponent<MeshRenderer>().sharedMaterial = mat;

        // Red eye lights
        SpawnEye(parent, new Vector3(-0.13f, 1.65f, 0.32f));
        SpawnEye(parent, new Vector3( 0.13f, 1.65f, 0.32f));
    }

    private static void SpawnEye(GameObject parent, Vector3 localPos)
    {
        var go = new GameObject("Eye");
        go.transform.SetParent(parent.transform, false);
        go.transform.localPosition = localPos;
        var lt        = go.AddComponent<Light>();
        lt.type       = LightType.Point;
        lt.color      = new Color(1f, 0.05f, 0.05f);
        lt.intensity  = 3f;
        lt.range      = 4f;
        lt.shadows    = LightShadows.None;
    }

    private static void BuildMinimapDot(GameObject parent)
    {
        // Red sphere high above geometry — visible only to top-down minimap camera
        var dot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        dot.name = "ChaserMinimapDot";
        dot.transform.SetParent(parent.transform, false);
        dot.transform.localPosition = Vector3.up * DotHeight;
        dot.transform.localScale    = Vector3.one * 1.6f;
        Object.Destroy(dot.GetComponent<Collider>());

        var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit")
                            ?? Shader.Find("Unlit/Color"));
        mat.color = new Color(1f, 0f, 0f);
        dot.GetComponent<MeshRenderer>().sharedMaterial = mat;
    }
}
