using UnityEngine;
using UnityEngine.InputSystem;

namespace MazeRunner
{
    /// <summary>Top-down character movement. Input: floating joystick on touch, WASD/arrows in the editor.</summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerController : MonoBehaviour
    {
        public const float RunSpeed = 5.6f;

        public bool InputEnabled;
        /// <summary>Editor automation (bot playthroughs) overrides player input when set.</summary>
        public static Vector2? BotInput;
        public Vector3 Velocity { get; private set; }
        public Vector2Int Cell => MazeView.WorldToCell(transform.position);

        CharacterController _cc;
        Animator _anim;
        float _vy;
        float _animSpeed;
        static readonly int SpeedId = Animator.StringToHash("Speed");
        static readonly int MotionId = Animator.StringToHash("MotionSpeed");
        static readonly int GroundedId = Animator.StringToHash("Grounded");
        static readonly int FreeFallId = Animator.StringToHash("FreeFall");

        /// <summary>Instantiates the bank's character, strips the Starter Assets scripts and attaches this controller.</summary>
        public static PlayerController Spawn(Transform parent, Vector3 position, ThemeAssets theme, float lightRange)
        {
            // Instantiate under an inactive holder so the Starter Assets scripts never run Awake.
            var holder = new GameObject("Holder");
            holder.SetActive(false);
            var go = Instantiate(AssetBank.I.Player, holder.transform);
            go.name = "Player";
            // PlayerInput is [RequireComponent]-ed by the Starter Assets controller, so it must go last.
            foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true))
                if (mb is not PlayerInput) DestroyImmediate(mb);
            foreach (var mb in go.GetComponentsInChildren<PlayerInput>(true)) DestroyImmediate(mb);
            foreach (var cam in go.GetComponentsInChildren<Camera>(true)) DestroyImmediate(cam.gameObject);

            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(position, Quaternion.identity);
            Destroy(holder);

            var pc = go.AddComponent<PlayerController>();
            var cc = go.GetComponent<CharacterController>();
            cc.radius = 0.35f;
            cc.height = 1.8f;
            cc.center = new Vector3(0, 0.93f, 0);
            cc.stepOffset = 0.25f;
            cc.skinWidth = 0.04f;

            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
            }

            // Torch: the only warm light in the maze.
            var torch = new GameObject("Torch").AddComponent<Light>();
            torch.transform.SetParent(go.transform, false);
            torch.transform.localPosition = new Vector3(0, 3.2f, 0.4f);
            torch.type = LightType.Point;
            torch.color = theme.Torch;
            torch.range = lightRange;
            torch.intensity = 9f;
            torch.shadows = LightShadows.None;
            torch.gameObject.AddComponent<TorchFlicker>();

            go.SetActive(true);
            return pc;
        }

        void Awake()
        {
            _cc = GetComponent<CharacterController>();
            _anim = GetComponent<Animator>();
            if (_anim != null) _anim.applyRootMotion = false;
        }

        void Update()
        {
            Vector2 input = InputEnabled ? ReadInput() : Vector2.zero;
            float mag = Mathf.Clamp01(input.magnitude);
            var dir = new Vector3(input.x, 0, input.y);
            if (dir.sqrMagnitude > 1f) dir.Normalize();

            var target = dir * RunSpeed;
            Velocity = Vector3.MoveTowards(Velocity, target, RunSpeed * 12f * Time.deltaTime);

            _vy = _cc.isGrounded ? -2f : _vy - 20f * Time.deltaTime;
            _cc.Move((Velocity + Vector3.up * _vy) * Time.deltaTime);

            if (dir.sqrMagnitude > 0.01f)
            {
                var rot = Quaternion.LookRotation(dir, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, rot, 1f - Mathf.Exp(-16f * Time.deltaTime));
            }

            if (_anim != null)
            {
                // Starter Assets blend tree: 2 = walk, 5.3 = sprint.
                float planar = new Vector3(Velocity.x, 0, Velocity.z).magnitude;
                _animSpeed = Mathf.Lerp(_animSpeed, planar / RunSpeed * 5.335f, 1f - Mathf.Exp(-12f * Time.deltaTime));
                _anim.SetFloat(SpeedId, _animSpeed);
                _anim.SetFloat(MotionId, mag > 0.05f ? Mathf.Max(0.6f, mag) : 1f);
                _anim.SetBool(GroundedId, true);
                _anim.SetBool(FreeFallId, false);
            }
        }

        static Vector2 ReadInput()
        {
            if (BotInput.HasValue) return BotInput.Value;
            var v = Joystick.Value;
            var kb = Keyboard.current;
            if (kb != null)
            {
                var k = Vector2.zero;
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) k.y += 1;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) k.y -= 1;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) k.x += 1;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) k.x -= 1;
                if (k != Vector2.zero) v = k.normalized;
            }
            return v;
        }

        /// <summary>Moves the player instantly (used when a rising wall would overlap them).</summary>
        public void Teleport(Vector3 p)
        {
            _cc.enabled = false;
            transform.position = p;
            _cc.enabled = true;
        }

        // Animation events baked into the Starter Assets clips — must have receivers or Unity logs errors.
        void OnFootstep(AnimationEvent e) { }
        void OnLand(AnimationEvent e) { }
    }

    public sealed class TorchFlicker : MonoBehaviour
    {
        Light _l; float _base, _seed;
        void Awake() { _l = GetComponent<Light>(); _base = _l.intensity; _seed = Random.value * 100f; }
        void Update()
        {
            float n = Mathf.PerlinNoise(_seed, Time.time * 3.2f);
            _l.intensity = _base * (0.86f + 0.22f * n);
        }
    }
}
