using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace MazeRunner.EditorTools
{
    /// <summary>Lists every asset the shipped game depends on (Main scene + AssetBank + Resources/DOTween), for safe cleanup.</summary>
    public static class DepAudit
    {
        public static void Run()
        {
            var roots = new[] { "Assets/_Project/Scenes/Main.unity", "Assets/_Project/Resources/AssetBank.asset", "Assets/Resources/DOTweenSettings.asset" };
            var deps = AssetDatabase.GetDependencies(roots, true).Where(p => !p.StartsWith("Packages/")).OrderBy(p => p).ToArray();
            File.WriteAllLines("Logs/mr_deps.txt", deps);
            Debug.Log($"[MR] DepAudit: {deps.Length} assets -> Logs/mr_deps.txt");
        }
    }
}
