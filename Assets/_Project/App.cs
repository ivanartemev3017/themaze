using DG.Tweening;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MazeRunner
{
    /// <summary>
    /// Entry point (the only object in the scene). Owns the camera, UI canvas, audio, store and the current world,
    /// and switches between menu, level select and gameplay without loading scenes.
    /// </summary>
    public sealed class App : MonoBehaviour
    {
        public static App I { get; private set; }

        public CameraRig Rig { get; private set; }
        public RectTransform Canvas { get; private set; }
        public World World { get; private set; }

        RectTransform _screen, _overlay;
        System.Action _back;
        Hud _hud;
        LevelConfig _current;
        int _levelsChapter;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoBoot()
        {
            if (I == null && Object.FindAnyObjectByType<App>() == null) new GameObject("App").AddComponent<App>();
        }

        void Awake()
        {
            if (I != null) { Destroy(gameObject); return; }
            I = this;
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            DOTween.Init(false, true, LogBehaviour.ErrorsOnly);
            DOTween.SetTweensCapacity(500, 100);
            Save.Load();

            Rig = CameraRig.Create(Color.black);
            Canvas = UIKit.CreateCanvas("UI", 0);
            Sfx.Create(transform);
            Store.Create(transform);
            Store.Unlocked += () => { Toast(Loc.Get("thanks")); Refresh(); };
            Store.Failed += msg => Toast(msg);
        }

        void Start() => ShowMenu();

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) Back();
        }

        void OnApplicationPause(bool paused)
        {
            if (paused && World != null && World.Level != null && World.Level.State == LevelController.Phase.Playing && _overlay == null)
                ShowPause();
        }

        public void Back()
        {
            if (_overlay != null) { CloseOverlay(); return; }
            _back?.Invoke();
        }

        // ── world ───────────────────────────────────────────────────────────────
        void BuildWorld(LevelConfig cfg, bool showcase)
        {
            if (World != null) Destroy(World.gameObject);
            DOTween.KillAll();
            Time.timeScale = 1f;
            World = World.Build(cfg, Rig, showcase);
        }

        // ── screens ─────────────────────────────────────────────────────────────
        RectTransform NewScreen(string name, System.Action onBack)
        {
            CloseOverlay();
            if (_screen != null) Destroy(_screen.gameObject);
            _hud = null;
            _screen = UIKit.Node(Canvas, name).Fill();
            _back = onBack;
            UIKit.FadeIn(_screen, 0.3f);
            return _screen;
        }

        public RectTransform NewOverlay(string name, bool dim = true)
        {
            CloseOverlay();
            _overlay = UIKit.Node(Canvas, name).Fill();
            if (dim)
            {
                var d = UIKit.Image(_overlay, "Dim", null, new Color(0, 0, 0, 0.62f), true);
                d.rectTransform.Fill();
            }
            UIKit.FadeIn(_overlay, 0.2f);
            return _overlay;
        }

        public void CloseOverlay()
        {
            if (_overlay == null) return;
            Destroy(_overlay.gameObject);
            _overlay = null;
            if (World != null && World.Level != null && World.Level.State is LevelController.Phase.Playing or LevelController.Phase.Intro)
            {
                World.Level.Paused = false;
                Time.timeScale = 1f;
            }
        }

        public void ShowMenu()
        {
            Time.timeScale = 1f;
            int ch = Mathf.Clamp(Save.D.LastChapter, 0, LevelConfig.ChapterCount - 1);
            if (World == null || World.Level != null) BuildWorld(LevelConfig.For(ch, 14), showcase: true);
            Screens.Menu(NewScreen("Menu", () => Application.Quit()));
        }

        public void ShowLevels(int chapter)
        {
            _levelsChapter = chapter;
            if (World == null || World.Level != null) BuildWorld(LevelConfig.For(chapter, 14), showcase: true);
            Screens.Levels(NewScreen("Levels", ShowMenu), chapter);
        }

        public void ShowSettings() => Screens.Settings(NewOverlay("Settings"));
        public void ShowUnlock() => Screens.Unlock(NewOverlay("Unlock"));

        public void Play(LevelConfig cfg)
        {
            _current = cfg;
            BuildWorld(cfg, showcase: false);
            var root = NewScreen("Game", ShowPause);
            _hud = Hud.Create(root, World.Level);
            World.Level.Finished += ShowResult;
            World.Level.Toast += Toast;
        }

        public void PlayNext()
        {
            if (_current.IsDaily) { ShowMenu(); return; }
            int ch = _current.Chapter, lv = _current.Level + 1;
            if (lv >= LevelConfig.LevelsPerChapter) { ch++; lv = 0; }
            if (ch >= LevelConfig.ChapterCount) { ShowLevels(_current.Chapter); return; }
            if (!Save.ChapterOpen(ch)) { ShowLevels(ch); ShowUnlock(); return; }
            Play(LevelConfig.For(ch, lv));
        }

        public void Retry() => Play(_current.IsDaily ? LevelConfig.Daily(System.DateTime.Now) : LevelConfig.For(_current.Chapter, _current.Level));

        public void ShowPause()
        {
            if (World?.Level == null || World.Level.State is LevelController.Phase.Won or LevelController.Phase.Lost) return;
            World.Level.Paused = true;
            Time.timeScale = 0f;
            Screens.Pause(NewOverlay("Pause"));
        }

        void ShowResult(LevelResult r)
        {
            Screens.Result(NewOverlay("Result"), r);
        }

        public void Toast(string text)
        {
            if (_hud != null) _hud.Toast(text);
            else Screens.FloatingToast(Canvas, text);
        }

        void Refresh()
        {
            if (_screen == null) return;
            if (_screen.name == "Levels") ShowLevels(_levelsChapter);
            if (_overlay != null && _overlay.name == "Unlock") CloseOverlay();
        }
    }
}
