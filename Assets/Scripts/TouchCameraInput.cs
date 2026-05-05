using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

/// <summary>
/// Reads touch drag on the RIGHT side of the screen to control camera yaw and pitch.
/// Uses New Input System (EnhancedTouch) — required because project has old Input disabled.
/// FollowCamera reads YawDelta / PitchDelta each frame and zeroes them.
/// </summary>
public class TouchCameraInput : MonoBehaviour
{
    [Header("Sensitivity")]
    public float sensitivity = 0.25f;

    [Header("Pitch Limits")]
    public float minPitch = -10f;
    public float maxPitch = 45f;

    public float YawDelta   { get; set; }
    public float PitchDelta { get; set; }

    private int     _activeFingerId = -1;
    private Vector2 _prevTouchPos;

    void OnEnable()
    {
        EnhancedTouchSupport.Enable();
    }

    void OnDisable()
    {
        EnhancedTouchSupport.Disable();
    }

    void Update()
    {
        HandleNewInputTouch();
        HandleMouse();
    }

    private void HandleNewInputTouch()
    {
        var touches = Touch.activeTouches;
        for (int i = 0; i < touches.Count; i++)
        {
            var t = touches[i];

            if (t.phase == TouchPhase.Began)
            {
                if (t.screenPosition.x > Screen.width * 0.5f && _activeFingerId == -1)
                {
                    _activeFingerId = t.touchId;
                    _prevTouchPos   = t.screenPosition;
                }
            }
            else if (t.touchId == _activeFingerId)
            {
                if (t.phase == TouchPhase.Moved)
                {
                    Vector2 delta = t.screenPosition - _prevTouchPos;
                    YawDelta   += delta.x * sensitivity;
                    PitchDelta -= delta.y * sensitivity;
                    _prevTouchPos = t.screenPosition;
                }
                else if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled)
                {
                    _activeFingerId = -1;
                }
            }
        }

        // Reset if active finger no longer exists
        if (_activeFingerId != -1)
        {
            bool found = false;
            for (int i = 0; i < touches.Count; i++)
                if (touches[i].touchId == _activeFingerId) { found = true; break; }
            if (!found) _activeFingerId = -1;
        }
    }

    private void HandleMouse()
    {
        var mouse = Mouse.current;
        if (mouse == null) return;

        // Right mouse button for editor camera control
        if (mouse.rightButton.isPressed)
        {
            Vector2 delta = mouse.delta.ReadValue();
            YawDelta   += delta.x * sensitivity * 0.5f;
            PitchDelta -= delta.y * sensitivity * 0.5f;
        }
    }
}
