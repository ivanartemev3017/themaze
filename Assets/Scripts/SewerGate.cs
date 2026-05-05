using UnityEngine;
using DG.Tweening;

public class SewerGate : MonoBehaviour
{
    [Header("Timing")]
    public float closedDuration = 10f;
    public float openDuration   = 6f;
    public float phaseOffset    = 0f;

    [Header("Animation")]
    public float openHeight  = 3.6f;
    public float closedY     = 0f;
    public float animTime    = 1.0f;

    BoxCollider _col;
    bool  _isOpen;
    float _timer;

    void Start()
    {
        _col   = GetComponent<BoxCollider>();
        _timer = phaseOffset % (closedDuration + openDuration);

        // Start closed
        transform.localPosition = new Vector3(transform.localPosition.x, closedY, transform.localPosition.z);
    }

    void Update()
    {
        _timer += Time.deltaTime;
        float phase = _isOpen ? openDuration : closedDuration;

        if (_timer >= phase)
        {
            _timer = 0f;
            Toggle();
        }
    }

    void Toggle()
    {
        _isOpen = !_isOpen;
        float targetY = _isOpen ? openHeight : closedY;

        transform.DOLocalMoveY(targetY, animTime)
            .SetEase(Ease.InOutCubic);

        // Collider: disable when opening, re-enable when fully closed
        if (_isOpen)
            _col.enabled = false;
        else
            DOVirtual.DelayedCall(animTime, () => { if (_col != null) _col.enabled = true; });
    }
}
