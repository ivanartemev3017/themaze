using System.Collections.Generic;
using UnityEngine;

/// Blind enemy that hunts purely by sound.
/// Player makes noise when moving (speed > noiseThreshold).
/// If player stands still for loseSoundDelay seconds — enemy gives up.
[RequireComponent(typeof(CharacterController))]
public class SewerEnemy : MonoBehaviour
{
    [Header("Detection")]
    public float hearingRadius     = 25f;
    public float noiseThreshold    = 0.4f;  // player CharacterController velocity magnitude
    public float loseSoundDelay    = 3.5f;  // seconds of silence before returning to patrol

    [Header("Movement")]
    public float patrolSpeed = 1.4f;
    public float chaseSpeed  = 3.2f;

    [Header("References")]
    public SewerMazeGenerator maze;

    // ── State ─────────────────────────────────────────────────────────────────
    enum State { Patrol, Chase, Search }
    State _state = State.Patrol;

    CharacterController _cc;
    Transform           _player;
    CharacterController _playerCC;

    Vector3 _lastHeardPos;
    float   _silenceTimer;
    Vector3 _patrolTarget;
    float   _patrolTimer;

    const float GRAVITY = 12f;
    float _vy;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Start()
    {
        _cc = GetComponent<CharacterController>();
        PlayerSpawner.OnPlayerSpawned += OnPlayerSpawned;
        PickPatrolTarget();
    }

    void OnDestroy() => PlayerSpawner.OnPlayerSpawned -= OnPlayerSpawned;

    void OnPlayerSpawned(GameObject p)
    {
        _player   = p.transform;
        _playerCC = p.GetComponent<CharacterController>();

        // Teleport to a random cell now that the maze exists
        if (maze != null)
        {
            var pos = maze.GetRandomCellCenter() + Vector3.up * 0.5f;
            _cc.enabled = false;
            transform.position = pos;
            _cc.enabled = true;
        }
        PickPatrolTarget();
    }

    // ── Update ────────────────────────────────────────────────────────────────

    void Update()
    {
        ApplyGravity();
        if (_player == null) return;

        float dist       = Vector3.Distance(transform.position, _player.position);
        float playerSpd  = _playerCC != null ? _playerCC.velocity.magnitude : 0f;
        bool  makesNoise = playerSpd > noiseThreshold && dist <= hearingRadius;

        switch (_state)
        {
            case State.Patrol:
                MoveToward(_patrolTarget, patrolSpeed);
                _patrolTimer -= Time.deltaTime;
                if (_patrolTimer <= 0f || Vector3.Distance(transform.position, _patrolTarget) < 1.2f)
                    PickPatrolTarget();

                if (makesNoise) Hear();
                break;

            case State.Chase:
                if (makesNoise)
                {
                    _lastHeardPos = _player.position;
                    _silenceTimer = 0f;
                }
                else
                {
                    _silenceTimer += Time.deltaTime;
                    if (_silenceTimer >= loseSoundDelay)
                    {
                        _state = State.Search;
                        break;
                    }
                }
                MoveToward(_lastHeardPos, chaseSpeed);

                if (dist < 1.1f)
                    SewerLevelManager.Instance?.PlayerCaught();
                break;

            case State.Search:
                MoveToward(_lastHeardPos, patrolSpeed);
                if (Vector3.Distance(transform.position, _lastHeardPos) < 1.2f)
                {
                    _state = State.Patrol;
                    PickPatrolTarget();
                }
                if (makesNoise) Hear();
                break;
        }
    }

    void Hear()
    {
        _lastHeardPos = _player.position;
        _silenceTimer = 0f;
        _state        = State.Chase;
    }

    void MoveToward(Vector3 target, float speed)
    {
        var dir = target - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.01f) return;

        dir.Normalize();
        _cc.Move(dir * speed * Time.deltaTime);
        transform.rotation = Quaternion.Slerp(transform.rotation,
            Quaternion.LookRotation(dir), 8f * Time.deltaTime);
    }

    void ApplyGravity()
    {
        if (_cc.isGrounded) _vy = -0.5f;
        else                _vy -= GRAVITY * Time.deltaTime;
        _cc.Move(new Vector3(0, _vy * Time.deltaTime, 0));
    }

    void PickPatrolTarget()
    {
        _patrolTarget = maze != null
            ? maze.GetRandomCellCenter()
            : transform.position + Random.insideUnitSphere * 8f;
        _patrolTarget.y  = transform.position.y;
        _patrolTimer = Random.Range(4f, 8f);
    }
}
