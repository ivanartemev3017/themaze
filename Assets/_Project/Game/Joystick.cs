using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace MazeRunner
{
    /// <summary>
    /// Floating joystick: touch anywhere that is not a button, drag to run. The ring appears under the finger.
    /// Mouse works too, so the editor behaves like a phone.
    /// </summary>
    public sealed class Joystick : MonoBehaviour
    {
        public static Vector2 Value { get; private set; }
        public static bool Active { get; private set; }

        const float Radius = 110f;
        RectTransform _ring, _knob, _rt;
        int _touchId = -1;
        Vector2 _origin;
        readonly List<RaycastResult> _hits = new();

        public static Joystick Create(RectTransform canvas)
        {
            var go = new GameObject("Joystick", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(canvas, false);
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            rt.SetAsFirstSibling();
            var j = go.AddComponent<Joystick>();
            j._rt = rt;
            j._ring = UIKit.Image(rt, "Ring", UIKit.RingSprite, new Color(1, 1, 1, 0.22f)).rectTransform;
            j._ring.sizeDelta = Vector2.one * Radius * 2.2f;
            j._knob = UIKit.Image(j._ring, "Knob", UIKit.CircleSprite, new Color(1, 1, 1, 0.45f)).rectTransform;
            j._knob.sizeDelta = Vector2.one * 96f;
            j._ring.gameObject.SetActive(false);
            return j;
        }

        void OnDisable() { Release(); }

        void Update()
        {
            var ts = Touchscreen.current;
            if (ts != null && ts.touches.Count > 0 && HandleTouches(ts)) return;
            HandleMouse();
        }

        bool HandleTouches(Touchscreen ts)
        {
            bool any = false;
            foreach (var t in ts.touches)
            {
                var phase = t.phase.ReadValue();
                int id = t.touchId.ReadValue();
                var pos = t.position.ReadValue();
                if (phase == UnityEngine.InputSystem.TouchPhase.None) continue;
                any = true;

                if (_touchId < 0 && phase == UnityEngine.InputSystem.TouchPhase.Began && !OverUI(pos)) Press(id, pos);
                if (id != _touchId) continue;

                if (phase is UnityEngine.InputSystem.TouchPhase.Ended or UnityEngine.InputSystem.TouchPhase.Canceled) Release();
                else Drag(pos);
            }
            if (!any && _touchId >= 0) Release();
            return any || _touchId >= 0;
        }

        void HandleMouse()
        {
            var m = Mouse.current;
            if (m == null) return;
            var pos = m.position.ReadValue();
            if (m.leftButton.wasPressedThisFrame && _touchId < 0 && !OverUI(pos)) Press(0, pos);
            if (_touchId < 0) return;
            if (m.leftButton.isPressed) Drag(pos); else Release();
        }

        void Press(int id, Vector2 screen)
        {
            _touchId = id;
            _origin = screen;
            Active = true;
            _ring.gameObject.SetActive(true);
            _ring.position = screen;
            _knob.anchoredPosition = Vector2.zero;
        }

        void Drag(Vector2 screen)
        {
            float scale = _rt.lossyScale.x <= 0 ? 1f : _rt.lossyScale.x;
            var delta = (screen - _origin) / scale;
            // Ring follows the finger when dragged beyond the edge — no dead stop.
            if (delta.magnitude > Radius)
            {
                var excess = delta - delta.normalized * Radius;
                _origin += excess * scale;
                _ring.position = _origin;
                delta = delta.normalized * Radius;
            }
            _knob.anchoredPosition = delta;
            var v = delta / Radius;
            Value = v.magnitude < 0.12f ? Vector2.zero : v;
        }

        void Release()
        {
            _touchId = -1;
            Value = Vector2.zero;
            Active = false;
            if (_ring != null) _ring.gameObject.SetActive(false);
        }

        bool OverUI(Vector2 screen)
        {
            var es = EventSystem.current;
            if (es == null) return false;
            var data = new PointerEventData(es) { position = screen };
            _hits.Clear();
            es.RaycastAll(data, _hits);
            return _hits.Count > 0;
        }
    }
}
