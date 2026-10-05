using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static MazeRunner.UIKit;

namespace MazeRunner
{
    /// <summary>In-game overlay: timer, shards, key, objective arrow, toasts, shift warning, joystick.</summary>
    public sealed class Hud : MonoBehaviour
    {
        LevelController _lv;
        RectTransform _root, _arrow, _toastRoot;
        TextMeshProUGUI _timer, _levelLabel;
        Image _timerBg, _key, _arrowImg, _shiftBar, _shiftBarBg;
        readonly List<Image> _shards = new();
        readonly Queue<string> _toasts = new();
        float _toastBusy;
        int _lastShards = -1;
        bool _lastKey;

        public static Hud Create(RectTransform root, LevelController lv)
        {
            var h = root.gameObject.AddComponent<Hud>();
            h._lv = lv;
            h._root = root;
            h.Build();
            return h;
        }

        void Build()
        {
            Joystick.Create(_root);

            var pause = IconButton(_root, Pause, App.I.ShowPause, 120);
            pause.GetComponent<RectTransform>().Place(new Vector2(0, 1), new Vector2(120, 120), new Vector2(44, -40));

            // Timer pill.
            _timerBg = Panel(_root, "Timer", new Color(0, 0, 0, 0.55f), false);
            _timerBg.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(330, 116), new Vector2(0, -36));
            var clock = Image(_timerBg.transform, "Clock", Clock, Ink);
            clock.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(58, 58), new Vector2(34, 0), new Vector2(0, 0.5f));
            _timer = Text(_timerBg.transform, "0:00", 72, Ink, title: true, TextAlignmentOptions.Center);
            _timer.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(230, 100), new Vector2(30, 0));

            // Next-shift bar under the timer.
            if (_lv.Cfg.ShiftInterval > 0)
            {
                _shiftBarBg = Panel(_root, "ShiftBg", new Color(0, 0, 0, 0.45f), false);
                _shiftBarBg.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(300, 14), new Vector2(0, -164));
                _shiftBar = Panel(_shiftBarBg.transform, "ShiftFill", new Color(0.35f, 0.75f, 1f, 0.85f), false);
                _shiftBar.rectTransform.anchorMin = Vector2.zero; _shiftBar.rectTransform.anchorMax = new Vector2(1, 1);
                _shiftBar.rectTransform.pivot = new Vector2(0, 0.5f);
                _shiftBar.rectTransform.offsetMin = _shiftBar.rectTransform.offsetMax = Vector2.zero;
            }

            string label = _lv.Cfg.IsDaily ? Loc.Get("daily") : $"{Loc.Get("level")} {_lv.Cfg.Level + 1}";
            _levelLabel = Text(_root, label, 30, Muted, title: true);
            _levelLabel.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(500, 44), new Vector2(0, -186));

            // Shards + key, top right.
            var bar = Panel(_root, "Collect", new Color(0, 0, 0, 0.55f), false);
            int slots = _lv.ShardsTotal + (_lv.Cfg.NeedKey ? 1 : 0);
            float w = 50 + slots * 76;
            bar.rectTransform.Place(new Vector2(1, 1), new Vector2(w, 116), new Vector2(-44, -36));
            for (int i = 0; i < _lv.ShardsTotal; i++)
            {
                var s = Image(bar.transform, "Shard", Diamond, StarEmpty);
                s.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(54, 64), new Vector2(36 + i * 76, 0), new Vector2(0, 0.5f));
                _shards.Add(s);
            }
            if (_lv.Cfg.NeedKey)
            {
                _key = Image(bar.transform, "Key", KeyIcon, StarEmpty);
                _key.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(70, 70), new Vector2(30 + _lv.ShardsTotal * 76, 0), new Vector2(0, 0.5f));
            }

            // Objective arrow.
            _arrowImg = Image(_root, "Objective", Arrow, Teal);
            _arrow = _arrowImg.rectTransform;
            _arrow.sizeDelta = new Vector2(72, 72);
            _arrow.anchorMin = _arrow.anchorMax = Vector2.zero;

            _toastRoot = Node(_root, "Toasts").Place(new Vector2(0.5f, 1), new Vector2(1200, 120), new Vector2(0, -240));
        }

        void Update()
        {
            if (_lv == null) return;

            // Timer.
            float t = _lv.State == LevelController.Phase.Intro ? _lv.Cfg.TimeLimit : _lv.TimeLeft;
            _timer.text = Loc.Time(Mathf.Ceil(t));
            bool low = t < 15f && _lv.State == LevelController.Phase.Playing;
            float pulse = low ? 0.5f + 0.5f * Mathf.Sin(Time.time * 10f) : 0f;
            _timer.color = Color.Lerp(Ink, Danger, low ? 0.6f + 0.4f * pulse : 0f);
            _timerBg.color = new Color(low ? 0.35f * pulse : 0f, 0f, 0f, 0.55f);

            // Shift bar: counts down to the next shift, flashes red during the warning.
            if (_shiftBar != null)
            {
                float k = Mathf.Clamp01(_lv.ShiftIn / Mathf.Max(1f, _lv.Cfg.ShiftInterval));
                _shiftBar.rectTransform.anchorMax = new Vector2(float.IsInfinity(_lv.ShiftIn) ? 0 : k, 1);
                bool warn = _lv.ShiftIn <= LevelController.WarnTime;
                _shiftBar.color = warn ? Color.Lerp(Danger, Ink, 0.5f + 0.5f * Mathf.Sin(Time.time * 18f)) : new Color(0.35f, 0.75f, 1f, 0.85f);
            }

            // Collectibles.
            if (_lv.ShardsGot != _lastShards)
            {
                for (int i = 0; i < _shards.Count; i++)
                {
                    bool got = i < _lv.ShardsGot;
                    _shards[i].color = got ? new Color(0.45f, 0.9f, 1f) : StarEmpty;
                    if (got && i == _lv.ShardsGot - 1 && _lastShards >= 0)
                        _shards[i].transform.DOPunchScale(Vector3.one * 0.5f, 0.4f, 6).SetLink(_shards[i].gameObject);
                }
                _lastShards = _lv.ShardsGot;
            }
            if (_key != null && _lv.HasKey != _lastKey)
            {
                _lastKey = _lv.HasKey;
                _key.color = _lastKey ? Gold : StarEmpty;
                _key.transform.DOPunchScale(Vector3.one * 0.5f, 0.4f, 6).SetLink(_key.gameObject);
            }

            UpdateArrow();
            UpdateToasts();
        }

        void UpdateArrow()
        {
            var cam = App.I.Rig.Cam;
            bool show = _lv.State == LevelController.Phase.Playing;
            var world = _lv.Objective + Vector3.up * 1f;
            var sp = cam.WorldToScreenPoint(world);
            var rect = _root.rect;
            float scale = _root.lossyScale.x <= 0 ? 1 : _root.lossyScale.x;
            var size = new Vector2(Screen.width, Screen.height);
            const float margin = 110f;
            bool onScreen = sp.z > 0 && sp.x > margin * scale && sp.x < size.x - margin * scale && sp.y > margin * scale && sp.y < size.y - margin * scale;
            _arrowImg.enabled = show && !onScreen;
            if (!_arrowImg.enabled) return;

            var center = size * 0.5f;
            var dir = ((Vector2)sp - center);
            if (sp.z < 0) dir = -dir;
            if (dir.sqrMagnitude < 1f) dir = Vector2.up;
            dir.Normalize();
            // Project to the screen border inset by margin.
            var half = center - Vector2.one * margin * scale;
            float tx = Mathf.Abs(dir.x) > 1e-4f ? half.x / Mathf.Abs(dir.x) : float.MaxValue;
            float ty = Mathf.Abs(dir.y) > 1e-4f ? half.y / Mathf.Abs(dir.y) : float.MaxValue;
            var pos = center + dir * Mathf.Min(tx, ty);
            _arrow.anchoredPosition = pos / scale;
            _arrow.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f);
            bool key = !_lv.Gate.Open;
            _arrowImg.color = (key ? Gold : Teal) * new Color(1, 1, 1, 0.75f + 0.25f * Mathf.Sin(Time.time * 5f));
        }

        public void Toast(string text)
        {
            if (_toasts.Count > 3) _toasts.Dequeue();
            _toasts.Enqueue(text);
        }

        void UpdateToasts()
        {
            if (_toastBusy > 0f) { _toastBusy -= Time.unscaledDeltaTime; return; }
            if (_toasts.Count == 0) return;
            var text = _toasts.Dequeue();
            var p = Panel(_toastRoot, "Toast", new Color(0, 0, 0, 0.72f), false);
            var tmp = Text(p.transform, text, 40, Ink);
            tmp.rectTransform.Fill(18);
            float width = Mathf.Min(1200f, tmp.GetPreferredValues(text, 1160f, 100f).x + 80f);
            p.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(width, 100f));
            var cg = p.gameObject.AddComponent<CanvasGroup>();
            float hold = Mathf.Clamp(text.Length * 0.055f, 1.4f, 3.6f);
            _toastBusy = hold + 0.4f;
            p.transform.localScale = Vector3.one * 0.85f;
            DOTween.Sequence().SetLink(p.gameObject)
                .Append(cg.DOFade(1, 0.18f).From(0))
                .Join(p.transform.DOScale(1f, 0.25f).SetEase(Ease.OutBack))
                .AppendInterval(hold)
                .Append(cg.DOFade(0, 0.35f))
                .OnComplete(() => Destroy(p.gameObject));
        }
    }
}
