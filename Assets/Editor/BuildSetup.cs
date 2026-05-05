using UnityEditor;
using UnityEngine;

/// <summary>
/// Adds MainMenu (index 0) and SampleScene (index 1) to Build Settings.
/// Run via: Tools → MazeRunner → Setup Build Scenes
/// </summary>
public static class BuildSetup
{
    [MenuItem("Tools/MazeRunner/Setup Build Scenes")]
    public static void SetupScenes()
    {
        var scenes = new[]
        {
            new EditorBuildSettingsScene("Assets/Scenes/MainMenu.unity",    true),
            new EditorBuildSettingsScene("Assets/Scenes/SampleScene.unity", true),
        };

        EditorBuildSettings.scenes = scenes;

        // Lock to landscape — prevents portrait mode on device
        PlayerSettings.defaultInterfaceOrientation           = UIOrientation.LandscapeLeft;
        PlayerSettings.allowedAutorotateToPortrait           = false;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        PlayerSettings.allowedAutorotateToLandscapeLeft      = true;
        PlayerSettings.allowedAutorotateToLandscapeRight     = true;

        Debug.Log("[BuildSetup] Build scenes set:\n  0 — MainMenu\n  1 — SampleScene\nOrientation locked to Landscape.");
    }
}
