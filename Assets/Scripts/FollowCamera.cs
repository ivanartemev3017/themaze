using UnityEngine;

/// <summary>
/// Third-person camera for a narrow-corridor maze game.
///
/// Key design decisions vs the old version:
///  • DefaultDistance 3.5 (was 5.0) — fits inside 4-unit corridors without
///    constantly hitting walls.
///  • SphereRadius 0.20 (was 0.40) — less aggressive, fires only on actual
///    wall contact instead of early phantom hits.
///  • Balanced lerp speeds 9 / 6 (was 16 / 3) — eliminates the oscillation
///    caused by fast snap-in vs very slow drift-out.
///  • MinDistance 1.5 — camera never clips through the player even in corners.
///  • HeightOffset 3.8 / Pitch 28° — slightly more overhead, so the line of
///    sight clears low wall geometry more often.
///  • Auto-follow unchanged: 0.8 s delay, 2.5 deg/s — tested, feels right.
/// </summary>
public class FollowCamera : MonoBehaviour
{
    public static FollowCamera Instance { get; private set; }

    // ---- tunables -------------------------------------------------------
    public float defaultDistance  = 3.5f;
    public float heightOffset     = 3.8f;
    public float defaultPitch     = 28f;
    private const float MinDistance      = 1.5f;
    private const float SphereRadius     = 0.20f;
    private const float SnapInSpeed      = 9f;
    private const float SnapOutSpeed     = 6f;
    private const float AutoFollowSpeed  = 2.5f;
    private const float AutoFollowDelay  = 0.8f;
    private const float AutoFollowMinSpd = 0.3f;
    // ---------------------------------------------------------------------

    private Transform           _target;
    private float               _yaw;
    private float               _pitch;
    private float               _currentDistance;
    private CharacterController _cc;
    private float               _lastInputTime;

    private TouchCameraInput _cameraInput;

    // Shake state
    private float _shakeStrength;
    private float _shakeDuration;
    private float _shakeEndTime;

    // First-person mode
    private bool                    _firstPerson;
    private SkinnedMeshRenderer[]   _playerMeshes;

    /// <summary>True when the camera is in first-person mode.</summary>
    public bool IsFirstPerson => _firstPerson;

    // =========================================================================
    void Awake()
    {
        if (Instance == null) Instance = this;
        _currentDistance = defaultDistance;
        _pitch           = defaultPitch;
    }

    /// <summary>Override camera tuning for a specific level (e.g. sewer tight corridors).</summary>
    public void SetCameraSettings(float newHeightOffset, float newDistance, float newPitch)
    {
        heightOffset     = newHeightOffset;
        defaultDistance  = newDistance;
        defaultPitch     = newPitch;
        _currentDistance = newDistance;
        _pitch           = newPitch;
    }

    void OnEnable()  => PlayerSpawner.OnPlayerSpawned += OnPlayerSpawned;
    void OnDisable() => PlayerSpawner.OnPlayerSpawned -= OnPlayerSpawned;

    void Start()
    {
        if (_target == null)
        {
            var p = GameObject.Find("Player");
            if (p != null) OnPlayerSpawned(p);
        }
    }

    void OnPlayerSpawned(GameObject player)
    {
        _target = player.transform;
        _yaw    = _target.eulerAngles.y;
        _cc     = player.GetComponent<CharacterController>();

        _cameraInput = FindAnyObjectByType<TouchCameraInput>();
        if (_cameraInput == null)
            _cameraInput = gameObject.AddComponent<TouchCameraInput>();

        _playerMeshes = player.GetComponentsInChildren<SkinnedMeshRenderer>(true);
    }

    // =========================================================================
    /// <summary>Toggle between third-person and first-person camera.</summary>
    public void ToggleCameraMode()
    {
        _firstPerson = !_firstPerson;
        // Hide player mesh in FP so we don't see ourselves from inside
        if (_playerMeshes != null)
            foreach (var smr in _playerMeshes)
                smr.enabled = !_firstPerson;
    }

    // =========================================================================
    /// <summary>Called by ShiftingWall to add a short positional shake.</summary>
    public void TriggerShake(float duration, float strength)
    {
        _shakeDuration = duration;
        _shakeStrength = strength;
        _shakeEndTime  = Time.time + duration;
    }

    // =========================================================================
    void LateUpdate()
    {
        // Fallback: if OnPlayerSpawned never fired (race condition / prefab issue),
        // find the player by name on every frame until found.
        if (_target == null)
        {
            var p = GameObject.Find("Player");
            if (p != null) OnPlayerSpawned(p);
            return;
        }

        // --- 1. Apply touch input -------------------------------------------
        // Consume input regardless of mode so pitch/yaw stay in sync
        if (_cameraInput != null)
        {
            float dy = _cameraInput.YawDelta;
            float dp = _cameraInput.PitchDelta;

            if (Mathf.Abs(dy) > 0.01f || Mathf.Abs(dp) > 0.01f)
                _lastInputTime = Time.time;

            _yaw   += dy;
            _pitch += dp;
            _pitch  = Mathf.Clamp(_pitch, _cameraInput.minPitch, _cameraInput.maxPitch);

            _cameraInput.YawDelta   = 0f;
            _cameraInput.PitchDelta = 0f;
        }

        // --- 1b. First-person: place camera at eye level and exit early ------
        if (_firstPerson)
        {
            transform.position = _target.position + Vector3.up * 1.7f;
            transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            return;
        }

        // --- 1c. Auto-follow behind player (3P only) -------------------------
        float timeSinceInput = Time.time - _lastInputTime;
        if (timeSinceInput > AutoFollowDelay && _cc != null &&
            _cc.velocity.magnitude > AutoFollowMinSpd)
        {
            float playerYaw = _target.eulerAngles.y;
            _yaw = Mathf.LerpAngle(_yaw, playerYaw, AutoFollowSpeed * Time.deltaTime);
        }

        Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0f);
        Vector3    pivot    = _target.position + Vector3.up * heightOffset;
        Vector3    backDir  = rotation * Vector3.back;

        // --- 2. SphereCast: detect walls ------------------------------------
        bool wallHit = Physics.SphereCast(
            pivot, SphereRadius, backDir, out RaycastHit hit, defaultDistance,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);

        float targetDist = wallHit
            ? Mathf.Max(MinDistance, hit.distance - SphereRadius)
            : defaultDistance;

        // Balanced lerp: fast snap in, moderately fast drift out.
        // Similar speeds prevent the oscillation where camera bounces
        // between "wall detected" and "wall clear" states.
        float lerpSpeed = targetDist < _currentDistance ? SnapInSpeed : SnapOutSpeed;
        _currentDistance = Mathf.Lerp(_currentDistance, targetDist, lerpSpeed * Time.deltaTime);

        // --- 3. Shake offset -----------------------------------------------
        Vector3 shakeOffset = Vector3.zero;
        float   remaining   = _shakeEndTime - Time.time;
        if (remaining > 0f && _shakeDuration > 0f)
        {
            float envelope = (remaining / _shakeDuration) * _shakeStrength;
            var   rand     = Random.insideUnitCircle * envelope;
            shakeOffset    = new Vector3(rand.x, 0f, rand.y);
        }

        // --- 4. Place camera -----------------------------------------------
        transform.position = pivot + backDir * _currentDistance + shakeOffset;
        transform.rotation = Quaternion.LookRotation(
            (_target.position + Vector3.up * 1.4f) - transform.position);
    }
}
