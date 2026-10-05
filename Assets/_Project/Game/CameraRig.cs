using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace MazeRunner
{
    /// <summary>Tilted top-down camera: follows the player, flies in at level start, pans slowly in the menu.</summary>
    public sealed class CameraRig : MonoBehaviour
    {
        public const float Pitch = 56f;
        public Camera Cam { get; private set; }

        enum Mode { Idle, Follow, Intro, Showcase }
        Mode _mode;
        Transform _target;
        PlayerController _player;
        Vector3 _focus, _focusVel;
        float _distance = 25f;
        float _shakeTime, _shakeDur, _shakeAmp;

        // intro
        Vector3 _introFrom; float _introFromDist, _introT, _introDur;
        // showcase
        Vector3 _center; float _showcaseRadius, _showcaseAngle;
        // follow bounds (maze rectangle) — keeps the void outside the maze off screen
        Vector3 _boundsMin, _boundsMax; bool _hasBounds;

        public void SetBounds(Vector3 min, Vector3 max) { _boundsMin = min; _boundsMax = max; _hasBounds = true; }

        Vector3 Clamp(Vector3 f)
        {
            if (!_hasBounds) return f;
            float halfV = _distance * Mathf.Tan(Cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float halfX = halfV * Cam.aspect * 0.92f;
            float halfZ = halfV / Mathf.Sin(Pitch * Mathf.Deg2Rad) * 0.8f;
            f.x = ClampAxis(f.x, _boundsMin.x, _boundsMax.x, halfX);
            f.z = ClampAxis(f.z, _boundsMin.z, _boundsMax.z, halfZ);
            return f;
        }

        static float ClampAxis(float v, float min, float max, float half)
        {
            const float slack = 3f; // allow a little of the outside so the outer wall reads
            if (max - min < 2f * (half - slack)) return (min + max) * 0.5f;
            return Mathf.Clamp(v, min + half - slack, max - half + slack);
        }

        public static CameraRig Create(Color background)
        {
            var go = new GameObject("Camera");
            go.tag = "MainCamera";
            var cam = go.AddComponent<Camera>();
            cam.fieldOfView = 38f;
            cam.nearClipPlane = 1f;
            cam.farClipPlane = 200f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = background;
            var data = go.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.renderShadows = false;
            go.AddComponent<AudioListener>();
            var rig = go.AddComponent<CameraRig>();
            rig.Cam = cam;
            return rig;
        }

        float BaseDistance()
        {
            // Keep roughly the same visible maze area on tall and wide screens.
            float aspect = Mathf.Max(1f, Cam.aspect);
            return Mathf.Lerp(24f, 20.5f, Mathf.InverseLerp(1.5f, 2.3f, aspect));
        }

        public void Follow(PlayerController player)
        {
            _player = player;
            _target = player.transform;
            _distance = BaseDistance();
            _focus = Clamp(_target.position);
            _distance = BaseDistance();
            _mode = Mode.Follow;
            Place();
        }

        /// <summary>Starts high above the whole maze and dives down to the player.</summary>
        public void Intro(PlayerController player, Vector3 mazeCenter, float mazeSpan, float duration)
        {
            Follow(player);
            _introFrom = mazeCenter;
            _introFromDist = Mathf.Max(30f, mazeSpan * 1.35f);
            _introT = 0f;
            _introDur = duration;
            _mode = Mode.Intro;
            Place();
        }

        public void Showcase(Vector3 center, float span)
        {
            _center = center;
            _showcaseRadius = span * 0.18f;
            _distance = Mathf.Max(28f, span * 0.7f);
            _mode = Mode.Showcase;
            _hasBounds = false;
            _target = null;
            _player = null;
        }

        public void Shake(float amplitude, float duration)
        {
            _shakeAmp = Mathf.Max(_shakeAmp * (_shakeTime > 0 ? 1 : 0), amplitude);
            _shakeDur = duration;
            _shakeTime = duration;
        }

        void LateUpdate()
        {
            switch (_mode)
            {
                case Mode.Follow:
                {
                    var lead = _player != null ? _player.Velocity * 0.28f : Vector3.zero;
                    _focus = Vector3.SmoothDamp(_focus, Clamp(_target.position + lead), ref _focusVel, 0.18f);
                    break;
                }
                case Mode.Intro:
                {
                    _introT += Time.deltaTime / _introDur;
                    if (_introT >= 1f) { _mode = Mode.Follow; break; }
                    float k = Mathf.SmoothStep(0f, 1f, Mathf.SmoothStep(0f, 1f, _introT));
                    _distance = Mathf.Lerp(_introFromDist, BaseDistance(), k);
                    _focus = Vector3.Lerp(_introFrom, Clamp(_target.position), k);
                    break;
                }
                case Mode.Showcase:
                {
                    _showcaseAngle += Time.deltaTime * 0.06f;
                    _focus = _center + new Vector3(Mathf.Cos(_showcaseAngle), 0, Mathf.Sin(_showcaseAngle * 1.3f)) * _showcaseRadius;
                    break;
                }
            }
            Place();
        }

        void Place()
        {
            var rot = Quaternion.Euler(Pitch, 0f, 0f);
            var pos = _focus - rot * Vector3.forward * _distance;
            if (_shakeTime > 0f)
            {
                _shakeTime -= Time.deltaTime;
                float a = _shakeAmp * (_shakeTime / _shakeDur);
                pos += new Vector3(Mathf.PerlinNoise(Time.time * 25f, 0f) - 0.5f, 0f, Mathf.PerlinNoise(0f, Time.time * 25f) - 0.5f) * a * 2f;
            }
            transform.SetPositionAndRotation(pos, rot);
        }
    }
}
