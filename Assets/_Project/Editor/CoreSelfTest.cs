using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MazeRunner.EditorTools
{
    /// <summary>Batchmode-runnable checks of the maze model. Logs lines prefixed with [MR].</summary>
    public static class CoreSelfTest
    {
        [MenuItem("MazeRunner/Run Core Self Test")]
        public static void Run()
        {
            int failures = 0, shifts = 0, swaps = 0;
            for (int ch = 0; ch < LevelConfig.ChapterCount; ch++)
            for (int lv = 0; lv < LevelConfig.LevelsPerChapter; lv++)
            {
                var cfg = LevelConfig.For(ch, lv);
                var m = MazeGen.Generate(cfg);
                if (!MazePath.AllConnected(m)) { failures++; Debug.LogError($"[MR] FAIL disconnected after gen ch{ch} lv{lv}"); continue; }
                if (m.Start == m.Exit) { failures++; Debug.LogError($"[MR] FAIL start==exit ch{ch} lv{lv}"); }

                var rng = new System.Random(cfg.Seed);
                var player = new List<Vector2Int> { m.Start };
                for (int i = 0; i < 300; i++)
                {
                    player[0] = new Vector2Int(rng.Next(m.W), rng.Next(m.H));
                    var plan = ShiftPlanner.Plan(m, rng, cfg.SwapsPerShift, player);
                    foreach (var s in plan)
                    {
                        if (!m.HasWall(s.Open) || m.HasWall(s.Close)) { failures++; Debug.LogError($"[MR] FAIL plan state ch{ch} lv{lv}"); }
                        m.SetWall(s.Open, false);
                        m.SetWall(s.Close, true);
                    }
                    shifts++; swaps += plan.Count;
                    if (!MazePath.AllConnected(m)) { failures++; Debug.LogError($"[MR] FAIL disconnected after shift ch{ch} lv{lv} i{i}"); break; }
                }
                if (lv % 10 == 0)
                    Debug.Log($"[MR] ch{ch} lv{lv} size {cfg.W}x{cfg.H} path {MazePath.Find(m, m.Start, m.Exit).Count} enemies {cfg.Enemies} key {cfg.NeedKey}");
            }
            Debug.Log($"[MR] CoreSelfTest done: {shifts} shifts, {swaps} swaps, failures={failures}");
        }
    }
}
