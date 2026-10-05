#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace MazeRunner.Dev
{
    /// <summary>
    /// Editor-only automation for verifying the game without a human: renders screenshots of any screen
    /// and lets a bot play levels. Driven by a command file written by EditorTools.Shots.
    /// Commands (one per line): menu | levels CH | play CH LV | daily | wait SEC | shot NAME | bot [TIMEOUT]
    ///                          | botall CH FROM TO | size W H | quit
    /// </summary>
    public sealed class ShotDirector : MonoBehaviour
    {
        public const string CommandFile = "Temp/mr_shots.txt";
        public static string OutDir = "Temp/mr_shots";

        RenderTexture _rt;
        float _deadline;

        void Awake()
        {
            _deadline = Time.realtimeSinceStartup + 900f;
            Application.logMessageReceived += (msg, stack, type) =>
            {
                if (type == LogType.Exception) Debug.Log("[MR] EXCEPTION " + msg + " | " + stack);
            };
        }

        void Update()
        {
            if (Time.realtimeSinceStartup > _deadline) { Debug.LogWarning("[MR] director watchdog timeout"); Quit(); }
        }
        int _w = 2340, _h = 1080;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (!File.Exists(CommandFile)) return;
            new GameObject("ShotDirector").AddComponent<ShotDirector>();
        }

        IEnumerator Start()
        {
            var lines = File.ReadAllLines(CommandFile);
            File.Delete(CommandFile);
            if (lines.Length > 0 && lines[0].StartsWith("out ")) { OutDir = lines[0].Substring(4).Trim(); }
            Directory.CreateDirectory(OutDir);
            yield return null;
            yield return null;
            Attach();

            foreach (var raw in lines)
            {
                var a = raw.Trim().Split(' ');
                if (a.Length == 0 || a[0].Length == 0 || a[0] == "out") continue;
                Debug.Log("[MR] shot cmd: " + raw);
                switch (a[0])
                {
                    case "size": _w = int.Parse(a[1]); _h = int.Parse(a[2]); Attach(); break;
                    case "menu": App.I.ShowMenu(); yield return Frames(3); break;
                    case "levels": App.I.ShowLevels(int.Parse(a[1])); yield return Frames(3); break;
                    case "play": App.I.Play(LevelConfig.For(int.Parse(a[1]), int.Parse(a[2]))); yield return Frames(2); Attach(); break;
                    case "daily": App.I.Play(LevelConfig.Daily(System.DateTime.Now)); yield return Frames(2); break;
                    case "wait": yield return new WaitForSeconds(float.Parse(a[1], System.Globalization.CultureInfo.InvariantCulture)); break;
                    case "shot": Shot(a[1]); break;
                    case "bot": yield return Bot(a.Length > 1 ? float.Parse(a[1]) : 240f); break;
                    case "botall": yield return BotAll(int.Parse(a[1]), int.Parse(a[2]), int.Parse(a[3])); break;
                    case "unlock": Save.FullUnlocked = true; break;
                    case "quit": Quit(); yield break;
                }
            }
            Quit();
        }

        static IEnumerator Frames(int n) { for (int i = 0; i < n; i++) yield return null; }

        void Attach()
        {
            if (_rt == null || _rt.width != _w || _rt.height != _h)
            {
                if (_rt != null) _rt.Release();
                _rt = new RenderTexture(_w, _h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            }
            var cam = App.I.Rig.Cam;
            cam.targetTexture = _rt;
            var canvas = App.I.Canvas.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 1.2f;
        }

        void Shot(string name)
        {
            Attach();
            var cam = App.I.Rig.Cam;
            Canvas.ForceUpdateCanvases();
            cam.Render();
            var prev = RenderTexture.active;
            RenderTexture.active = _rt;
            var tex = new Texture2D(_w, _h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, _w, _h), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            var path = Path.Combine(OutDir, name + ".png");
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Destroy(tex);
            Debug.Log("[MR] shot saved " + Path.GetFullPath(path));
        }

        // ── bot ─────────────────────────────────────────────────────────────────
        LevelResult _last;

        IEnumerator Bot(float timeout)
        {
            var lv = App.I.World.Level;
            _last = null;
            lv.Finished += r => _last = r;
            float t = 0;
            while (_last == null && t < timeout)
            {
                PlayerController.BotInput = Steer(lv);
                t += Time.deltaTime;
                yield return null;
            }
            PlayerController.BotInput = null;
            if (_last == null) Debug.LogWarning($"[MR] bot TIMEOUT ch{lv.Cfg.Chapter} lv{lv.Cfg.Level} state={lv.State}");
            else Debug.Log($"[MR] bot ch{lv.Cfg.Chapter} lv{lv.Cfg.Level + 1}: {(_last.Won ? "WON" : "LOST " + _last.Reason)} in {_last.Time:F1}s (limit {lv.Cfg.TimeLimit}, par {lv.Cfg.ParTime}) stars={_last.Stars} size={lv.Cfg.W}x{lv.Cfg.H}");
        }

        IEnumerator BotAll(int ch, int from, int to)
        {
            Time.timeScale = 1f;
            int won = 0, total = 0;
            for (int i = from; i <= to; i++)
            {
                App.I.Play(LevelConfig.For(ch, i));
                yield return Frames(2);
                Attach();
                yield return Bot(400f);
                total++; if (_last != null && _last.Won) won++;
                App.I.CloseOverlay();
            }
            Debug.Log($"[MR] botall ch{ch} {from}..{to}: won {won}/{total}");
        }

        static Vector2 Steer(LevelController lv)
        {
            if (lv.State != LevelController.Phase.Playing) return Vector2.zero;
            var m = lv.Maze;
            var p = lv.Player.transform.position;
            var cell = lv.Player.Cell;
            Vector2Int goal = m.Exit;
            if (!lv.Gate.Open && lv.Key != null) goal = MazeView.WorldToCell(lv.Key.transform.position);

            Vector3 target;
            if (cell == goal)
                target = goal == m.Exit ? lv.Gate.Threshold + lv.Gate.Outward * 2f : MazeView.CellCenter(goal);
            else
            {
                var path = MazePath.Find(m, cell, goal);
                if (path == null || path.Count < 2) return Vector2.zero;
                var next = MazeView.CellCenter(path[1]);
                var here = MazeView.CellCenter(cell);
                // Stay near the corridor centre line while turning.
                var axis = next - here;
                var off = p - here;
                bool lateralOk = Mathf.Abs(axis.x) > 0.1f ? Mathf.Abs(off.z) < 0.6f : Mathf.Abs(off.x) < 0.6f;
                target = lateralOk ? next : here;
            }
            var d = target - p; d.y = 0;
            return d.sqrMagnitude < 0.01f ? Vector2.zero : new Vector2(d.x, d.z).normalized;
        }

        static void Quit()
        {
            Debug.Log("[MR] ShotDirector finished");
            UnityEditor.EditorApplication.Exit(0);
        }
    }
}
#endif
