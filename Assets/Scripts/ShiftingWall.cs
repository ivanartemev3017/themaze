using UnityEngine;
using DG.Tweening;

/// <summary>
/// A wall segment that can slide to an alternate position on command from MazeShifter.
/// Slides along transform.right by ShiftDistance units, toggling between posA (original)
/// and posB (shifted). Triggered by a global timer — not by player proximity.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class ShiftingWall : MonoBehaviour
{
    // Set by MazeGenerator immediately after AddComponent
    [HideInInspector] public float ShiftDistance = 4f;  // = cellSize

    [Header("Movement")]
    public float ShiftDuration = 0.8f;

    [Header("Camera Shake")]
    public float ShakeDuration = 0.30f;
    public float ShakeStrength = 0.15f;

    // -----------------------------------------------------------------
    private Vector3 _posA;       // original spawned position
    private Vector3 _posB;       // alternate position (posA + right * ShiftDistance)
    private bool    _atA = true;
    private Tween   _tween;
    private AudioSource _audio;

    // =================================================================
    void Start()
    {
        _posA = transform.position;
        _posB = _posA + transform.right * ShiftDistance;

        _audio              = gameObject.AddComponent<AudioSource>();
        _audio.spatialBlend = 1f;
        _audio.maxDistance  = 25f;
        _audio.rolloffMode  = AudioRolloffMode.Linear;
    }

    void OnDestroy() => _tween?.Kill();

    // =================================================================
    /// <summary>Called by MazeShifter on a global timer to slide wall to the other position.</summary>
    public void ShiftToOther()
    {
        _tween?.Kill();

        _atA   = !_atA;
        Vector3 target = _atA ? _posA : _posB;

        _tween = transform.DOMove(target, ShiftDuration).SetEase(Ease.InOutCubic);

        Camera.main?.GetComponent<FollowCamera>()?.TriggerShake(ShakeDuration, ShakeStrength);
        SoundManager.Instance?.PlayRumble(transform.position);
    }
}
