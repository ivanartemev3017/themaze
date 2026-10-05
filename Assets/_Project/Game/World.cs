using UnityEngine;
using UnityEngine.Rendering;

namespace MazeRunner
{
    /// <summary>Builds and owns everything in the 3D scene for one maze: geometry, lighting, actors.</summary>
    public sealed class World : MonoBehaviour
    {
        public MazeData Maze { get; private set; }
        public MazeView View { get; private set; }
        public LevelController Level { get; private set; }
        public ThemeAssets Theme { get; private set; }

        public static World Build(LevelConfig cfg, CameraRig rig, bool showcase)
        {
            var go = new GameObject(showcase ? "World (showcase)" : "World");
            var w = go.AddComponent<World>();
            w.Theme = AssetBank.I.Theme(cfg.ThemeIndex);
            w.Maze = MazeGen.Generate(cfg);

            var viewGo = new GameObject("Maze");
            viewGo.transform.SetParent(go.transform, false);
            w.View = viewGo.AddComponent<MazeView>();
            w.View.Build(w.Maze, w.Theme);

            ApplyLighting(go.transform, w.Theme);
            rig.Cam.backgroundColor = w.Theme.Background;

            if (showcase)
            {
                go.AddComponent<Showcase>().Init(w.Maze, w.View, rig, cfg.Seed);
                rig.Showcase(w.View.Center, Mathf.Max(w.Maze.W, w.Maze.H) * MazeView.Cell);
                ExitGate.Spawn(go.transform, w.Maze, w.Theme, false);
            }
            else
            {
                var levelGo = new GameObject("Level");
                levelGo.transform.SetParent(go.transform, false);
                w.Level = levelGo.AddComponent<LevelController>();
                w.Level.Setup(cfg, w.Maze, w.View, rig, w.Theme);
            }
            return w;
        }

        static void ApplyLighting(Transform parent, ThemeAssets theme)
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = theme.Ambient;
            RenderSettings.fog = false;
            RenderSettings.skybox = null;

            var moon = new GameObject("Moon").AddComponent<Light>();
            moon.transform.SetParent(parent, false);
            moon.type = LightType.Directional;
            moon.transform.rotation = Quaternion.Euler(62f, -35f, 0f);
            moon.color = theme.Moon;
            moon.intensity = theme.MoonIntensity;
            moon.shadows = LightShadows.None;

            var bank = AssetBank.I;
            if (bank.Profile != null)
            {
                var vol = new GameObject("Volume").AddComponent<Volume>();
                vol.transform.SetParent(parent, false);
                vol.isGlobal = true;
                vol.priority = 10;
                vol.sharedProfile = bank.Profile;
            }
        }
    }

    /// <summary>Menu background: the maze keeps rearranging itself while the camera drifts.</summary>
    public sealed class Showcase : MonoBehaviour
    {
        MazeData _m; MazeView _v; CameraRig _rig; System.Random _rng;
        float _t = 2.5f;
        System.Collections.Generic.List<ShiftPlanner.Swap> _plan;

        public void Init(MazeData m, MazeView v, CameraRig rig, int seed) { _m = m; _v = v; _rig = rig; _rng = new System.Random(seed); }

        void Update()
        {
            _t -= Time.deltaTime;
            if (_plan == null && _t < 1.4f)
            {
                _plan = ShiftPlanner.Plan(_m, _rng, 3, null);
                _v.ShowWarning(_plan, 1.4f);
            }
            if (_t <= 0f)
            {
                _v.ClearWarnings();
                foreach (var s in _plan) { _m.SetWall(s.Open, false); _m.SetWall(s.Close, true); }
                _plan = null;
                _rig.Shake(0.12f, 0.3f);
                _t = 3.2f;
            }
        }
    }
}
