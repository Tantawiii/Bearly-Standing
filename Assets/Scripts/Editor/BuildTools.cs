using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace BearlyStanding.EditorTools
{
    /// <summary>One-click PC (Windows x64) and WebGL builds into the repo's <c>Builds/</c> folder.</summary>
    public static class BuildTools
    {
        private static string[] Scenes()
        {
            var list = new System.Collections.Generic.List<string>();
            foreach (var s in EditorBuildSettings.scenes)
                if (s.enabled) list.Add(s.path);
            return list.ToArray();
        }

        [MenuItem("Bearly Standing/Build Player/Windows x64", priority = 60)]
        public static void BuildWindows()
        {
            string dir = Path.GetFullPath("Builds/Windows");
            Directory.CreateDirectory(dir);
            Run(new BuildPlayerOptions
            {
                scenes = Scenes(),
                locationPathName = Path.Combine(dir, "BearlyStanding.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            });
        }

        [MenuItem("Bearly Standing/Build Player/WebGL", priority = 61)]
        public static void BuildWebGL()
        {
            string dir = Path.GetFullPath("Builds/WebGL");
            Directory.CreateDirectory(dir);
            Run(new BuildPlayerOptions
            {
                scenes = Scenes(),
                locationPathName = dir,
                target = BuildTarget.WebGL,
                options = BuildOptions.None
            });
        }

        private static void Run(BuildPlayerOptions options)
        {
            if (options.scenes == null || options.scenes.Length == 0)
            {
                Debug.LogError("BuildTools: no enabled scenes in Build Settings. Run 'Build Everything' first.");
                return;
            }

            BuildReport report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            if (summary.result == BuildResult.Succeeded)
                Debug.Log($"BuildTools: {options.target} build OK → {summary.outputPath}  ({summary.totalSize / 1048576} MB, {summary.totalTime.TotalSeconds:F0}s)");
            else
                Debug.LogError($"BuildTools: {options.target} build {summary.result} — {summary.totalErrors} errors.");
        }
    }
}
