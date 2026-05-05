using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Floating virtual joystick for mobile.
/// The background appears where the finger touches; the thumb follows within _range pixels.
///
/// Setup in Unity:
///   1. Create: GameObject → UI → Canvas (Screen Space - Overlay)
///      Name it "HUDCanvas". Set Canvas Scaler → Scale With Screen Size → 1080x1920.
///   2. Inside HUDCanvas create an empty GameObject named "Joystick".
///      Add this script (ScreenJoystick) to it.
///      Set Anchor to bottom-left, stretch to cover left half of screen.
///      Enable Raycast Target on the RectTransform (default is on).
///   3. Inside "Joystick" create two child Image GameObjects:
///      - "Background": circle sprite, ~120px, alpha 0.4, color dark grey.
///      - "Thumb"      : circle sprite, ~60px,  alpha 0.7, color white.
///   4. Assign Background and Thumb RectTransforms to this component in Inspector.
///   5. Set Background to inactive (SetActive false) in the scene — it appears on touch.
/// </summary>
public class ScreenJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [Tooltip("Background ring RectTransform — positioned where finger touches.")]
    [SerializeField] private RectTransform _background;

    [Tooltip("Thumb (inner circle) RectTransform — moves within the background.")]
    [SerializeField] private RectTransform _thumb;

    [Tooltip("Maximum thumb travel in canvas pixels.")]
    [SerializeField] private float _range = 60f;

    /// <summary>Normalised input direction, magnitude 0..1.</summary>
    public Vector2 Direction { get; private set; }

    // -------------------------------------------------------------------------
    private Canvas     _canvas;
    private Vector2    _origin;   // background anchoredPosition when finger pressed

    // =========================================================================
    // Unity messages
    // =========================================================================

    private void Awake()
    {
        _canvas = GetComponentInParent<Canvas>();
        if (_background != null) _background.gameObject.SetActive(false);
    }

    /// <summary>Called by MobileControls after building background and thumb at runtime.</summary>
    public void Init(RectTransform background, RectTransform thumb)
    {
        _background = background;
        _thumb      = thumb;
    }

    // =========================================================================
    // IPointer / IDrag handlers
    // =========================================================================

    public void OnPointerDown(PointerEventData eventData)
    {
        _origin = ScreenToCanvas(eventData.position);
        _background.anchoredPosition = _origin;
        _background.gameObject.SetActive(true);
        _thumb.anchoredPosition = _origin;
        OnDrag(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        Vector2 current = ScreenToCanvas(eventData.position);
        Vector2 delta   = Vector2.ClampMagnitude(current - _origin, _range);
        _thumb.anchoredPosition = _origin + delta;
        Direction = delta / _range;
    }

    public void OnPointerUpHandler(PointerEventData eventData) => OnPointerUp(eventData);

    public void OnPointerUp(PointerEventData eventData)
    {
        Direction = Vector2.zero;
        _thumb.anchoredPosition = _origin;
        _background.gameObject.SetActive(false);
    }

    // =========================================================================
    // Helper
    // =========================================================================

    private Vector2 ScreenToCanvas(Vector2 screenPoint)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _canvas.transform as RectTransform,
            screenPoint,
            _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera,
            out Vector2 local);
        return local;
    }
}
