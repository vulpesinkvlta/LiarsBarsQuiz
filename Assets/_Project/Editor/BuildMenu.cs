using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace LiarsBar.Editor
{
    /// <summary>
    /// Пункты меню для WebGL-сборок. Результат пишется в консоль и в Temp/liarsbar_build.txt.
    /// </summary>
    public static class BuildMenu
    {
        private const string SmokeTestScene = "Assets/_Project/Networking/SmokeTest/PhotonSmokeTest.unity";
        private const string ResultFile = "Temp/liarsbar_build.txt";

        [MenuItem("LiarsBar/Build/Smoke Test (WebGL)")]
        public static void BuildSmokeTest() => Build(new[] { SmokeTestScene }, "Builds/SmokeTest");

        [MenuItem("LiarsBar/Build/Game (WebGL)")]
        public static void BuildGame() =>
            Build(EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(), "Builds/Game");

        private static void Build(string[] scenes, string location)
        {
            if (File.Exists(ResultFile)) File.Delete(ResultFile);
            string text;
            try
            {
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = location,
                    target = BuildTarget.WebGL,
                    options = BuildOptions.None
                });
                var s = report.summary;
                text = $"{s.result} path={location} size={s.totalSize / (1024 * 1024)}MB time={(int)s.totalTime.TotalSeconds}s errors={s.totalErrors} warnings={s.totalWarnings}";
                foreach (var m in report.steps.SelectMany(st => st.messages))
                    if (m.type == LogType.Error || m.type == LogType.Exception) text += "\nERR: " + m.content;
            }
            catch (System.Exception e)
            {
                text = "EXCEPTION " + e;
            }
            File.WriteAllText(ResultFile, text);
            Debug.Log("[Build] " + text);
        }
    }
}
