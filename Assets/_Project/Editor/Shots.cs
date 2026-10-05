using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace MazeRunner.EditorTools
{
    /// <summary>
    /// Enters play mode with a ShotDirector script. Pass commands with -mrcmd "cmd1;cmd2;..." on the command line.
    /// Run WITHOUT -quit; the director exits the editor when finished.
    /// </summary>
    public static class Shots
    {
        public static void Run()
        {
            string cmds = "menu;wait 2;shot menu;quit";
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-mrcmd") cmds = args[i + 1];

            Directory.CreateDirectory("Temp");
            File.WriteAllText(Dev.ShotDirector.CommandFile, cmds.Replace(';', '\n'));
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Main.unity");
            EditorApplication.isPlaying = true;
        }
    }
}
