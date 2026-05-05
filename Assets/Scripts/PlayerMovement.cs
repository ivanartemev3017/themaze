using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Simple CharacterController-based movement for MazeRunner.
/// Works with keyboard (editor) and ScreenJoystick (mobile).
/// Reads camera-relative direction so the joystick always feels
/// "forward = away from camera", regardless of camera angle.
///
/// Attach to the Player GameObject (requires CharacterController).
/// Call SetJoystick() from PlayerSpawner after spawning.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed     = 4f;
    public float rotationSpeed = 12f;  // fast rotation = responsive feel

    [Header("Physics")]
    public float gravity = -20f;

    // -------------------------------------------------------------------------
    private const float DeadZone   = 0.12f;
    private const float SmoothTime = 0.04f;

    private CharacterController _cc;
    private Animator            _animator;
    private Joystick            _joystick;        // Joystick Pack base class (FloatingJoystick)
    private float               _verticalVelocity;
    private Vector2             _smoothedInput;   // smoothed joystick direction
    private Vector2             _smoothVelocity;  // SmoothDamp internal velocity

    private static readonly int s_Speed       = Animator.StringToHash("Speed");
    private static readonly int s_Grounded    = Animator.StringToHash("Grounded");
    private static readonly int s_FreeFall    = Animator.StringToHash("FreeFall");
    private static readonly int s_MotionSpeed = Animator.StringToHash("MotionSpeed");

    // =========================================================================
    // Unity messages
    // =========================================================================

    private void Awake()
    {
        _cc       = GetComponent<CharacterController>();
        _animator = GetComponentInChildren<Animator>();
        if (_animator != null)
            _animator.applyRootMotion = false;
    }

    private void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.State != GameManager.GameState.Playing) return;

        ApplyGravity();

        Vector2 input = ReadInput();
        Vector3 move  = InputToWorldDirection(input);

        _cc.Move((move * moveSpeed + Vector3.up * _verticalVelocity) * Time.deltaTime);

        if (move.sqrMagnitude > 0.01f)
            RotateToward(move);

        UpdateAnimator(move.magnitude);
    }

    // =========================================================================
    // Public API
    // =========================================================================

    /// <summary>Wire the scene joystick after the player is spawned.</summary>
    public void SetJoystick(Joystick joystick) => _joystick = joystick;

    // =========================================================================
    // Private
    // =========================================================================

    private void ApplyGravity()
    {
        if (_cc.isGrounded && _verticalVelocity < 0f)
            _verticalVelocity = -2f;
        _verticalVelocity += gravity * Time.deltaTime;
    }

    private Vector2 ReadInput()
    {
        // Joystick (mobile) — FloatingJoystick already zeroes below its own dead zone,
        // but we apply our own so keyboard input is treated identically.
        Vector2 input = _joystick != null ? _joystick.Direction : Vector2.zero;

        // Keyboard fallback for editor testing
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed)  input.x -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) input.x += 1f;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed)  input.y -= 1f;
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed)    input.y += 1f;
        }

        Vector2 raw = Vector2.ClampMagnitude(input, 1f);
        if (raw.magnitude < DeadZone) raw = Vector2.zero;

        // SmoothDamp gives consistent, frame-rate-independent acceleration and
        // deceleration — avoids the "snap stop" and "mushy start" from Lerp.
        _smoothedInput = Vector2.SmoothDamp(
            _smoothedInput, raw, ref _smoothVelocity, SmoothTime);

        // Mild power curve (1.3): keeps near-linear feel at full tilt,
        // adds slight control at small deflections without mushy starts.
        float mag = _smoothedInput.magnitude;
        return mag > 0.001f ? (_smoothedInput / mag) * Mathf.Pow(mag, 1.3f) : Vector2.zero;
    }

    private static Vector3 InputToWorldDirection(Vector2 input)
    {
        // Move relative to camera's horizontal orientation so W always means
        // "away from camera" regardless of which way the player faces.
        Camera cam = Camera.main;
        if (cam == null) return new Vector3(input.x, 0f, input.y);

        Vector3 forward = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized;
        Vector3 right   = Vector3.ProjectOnPlane(cam.transform.right,   Vector3.up).normalized;

        // forward is non-zero because the third-person camera always has a
        // horizontal component (it is never pointing straight down).
        return forward * input.y + right * input.x;
    }

    private void RotateToward(Vector3 direction)
    {
        Quaternion target = Quaternion.LookRotation(direction);
        transform.rotation = Quaternion.Slerp(
            transform.rotation, target, rotationSpeed * Time.deltaTime);
    }

    private void UpdateAnimator(float inputMagnitude)
    {
        if (_animator == null) return;
        // StarterAssets blend tree expects speed in m/s range (0=idle, ~2=walk, ~6=run)
        float speed = inputMagnitude * moveSpeed;
        _animator.SetFloat(s_Speed,       speed, 0.1f, Time.deltaTime);
        _animator.SetFloat(s_MotionSpeed, 1f);
        _animator.SetBool(s_Grounded,     _cc.isGrounded);
        _animator.SetBool(s_FreeFall,     !_cc.isGrounded && _verticalVelocity < -2f);
    }

    // Suppress StarterAssets AnimationEvent warnings — ThirdPersonController is disabled
    private void OnFootstep(AnimationEvent evt) { }
    private void OnLand(AnimationEvent evt) { }
}
