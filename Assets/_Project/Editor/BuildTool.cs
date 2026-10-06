using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace MazeRunner.EditorTools
{
    /// <summary>
    /// Verification build: debug-signed APK in Builds/ so build errors surface without the release keystore.
    /// The project's keystore settings are restored afterwards. Release builds are made from File → Build Profiles.
    /// </summary>
    public static class BuildTool
    {
        public static void TestApk()
        {
            bool custom = PlayerSettings.Android.useCustomKeystore;
            bool aab = EditorUserBuildSettings.buildAppBundle;
            try
            {
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
                PlayerSettings.Android.useCustomKeystore = false;
                EditorUserBuildSettings.buildAppBundle = false;
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { "Assets/_Project/Scenes/Main.unity" },
                    locationPathName = "Builds/TheMaze-test.apk",
                    target = BuildTarget.Android,
                    options = BuildOptions.None,
                });
                var s = report.summary;
                Debug.Log($"[MR] build {s.result}: {s.totalSize / (1024f * 1024f):F1} MB, {s.totalErrors} errors, {s.totalTime}");
                if (s.result != BuildResult.Succeeded)
                    foreach (var step in report.steps)
                    foreach (var m in step.messages)
                        if (m.type is LogType.Error or LogType.Exception) Debug.Log("[MR] build error: " + m.content);
            }
            finally
            {
                PlayerSettings.Android.useCustomKeystore = custom;
                EditorUserBuildSettings.buildAppBundle = aab;
                AssetDatabase.SaveAssets();
            }
        }
    }
}
