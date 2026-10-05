using DG.Tweening;
using UnityEngine;

namespace MazeRunner
{
    /// <summary>A shiftable wall segment. Raised = solid wall, lowered = hidden under the floor.</summary>
    public sealed class WallSlab : MonoBehaviour
    {
        const float Down = -(MazeView.WallH + 0.1f);
        const float RiseTime = 0.55f;
        const float SinkTime = 0.7f;

        public Edge Edge { get; private set; }
        public bool Up { get; private set; }

        BoxCollider _col;
        MeshRenderer _renderer;
        Vector3 _base;

        public void Init(Edge e, bool up)
        {
            Edge = e;
            Up = up;
            _base = transform.localPosition;
            _renderer = GetComponent<MeshRenderer>();
            var (min, max) = MazeView.WallBounds(new Edge(e.Vertical, 0, 0));
            var c = MazeView.EdgeCenter(new Edge(e.Vertical, 0, 0));
            _col = gameObject.AddComponent<BoxCollider>();
            _col.center = (min + max) * 0.5f - c;
            _col.size = max - min;
            Apply(up ? 0f : Down);
            _col.enabled = up;
            _renderer.enabled = up;
        }

        public void Set(bool up)
        {
            if (Up == up) return;
            Up = up;
            DOTween.Kill(transform);
            transform.localPosition = new Vector3(_base.x, transform.localPosition.y, _base.z);
            _renderer.enabled = true;
            if (up)
            {
                _col.enabled = true; // solid immediately; LevelController keeps actors out of the way
                transform.DOLocalMoveY(_base.y, RiseTime).SetEase(Ease.OutCubic).SetTarget(transform);
            }
            else
            {
                transform.DOLocalMoveY(_base.y + Down, SinkTime).SetEase(Ease.InCubic).SetTarget(transform)
                    .OnUpdate(() => { if (transform.localPosition.y < _base.y - MazeView.WallH * 0.5f) _col.enabled = false; })
                    .OnComplete(() => { _col.enabled = false; _renderer.enabled = false; });
            }
        }

        /// <summary>Shakes a raised wall that is about to sink — the visual "it's going to open" cue.</summary>
        public void Tremble(float duration)
        {
            if (!Up) return;
            transform.DOShakePosition(duration, new Vector3(0.06f, 0.02f, 0.06f), 30, 90, false, false).SetTarget(transform)
                .OnComplete(() => transform.localPosition = new Vector3(_base.x, transform.localPosition.y, _base.z));
        }

        void Apply(float y) => transform.localPosition = _base + new Vector3(0, y, 0);

        void OnDestroy() => DOTween.Kill(transform);
    }
}
