using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace EchoProtocol.Editor
{
    public static class WebGLReleaseBuilder
    {
        private const string MenuPath = "Tools/ECHO Protocol/Build WebGL Release";

        [MenuItem(MenuPath)]
        public static void BuildFromMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            {
                Debug.LogError("WebGL build requires the Editor to be out of Play mode and finished compiling.");
                return;
            }

            // Let the menu command return before the long-running build starts.
            EditorApplication.delayCall += Build;
        }

        [MenuItem(MenuPath, true)]
        private static bool ValidateBuildFromMenu()
        {
            return !EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling;
        }

        public static void Build()
        {
            var scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();
            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            if (scenes.Length == 0 || scenes.Any(scene => !File.Exists(Path.Combine(projectRoot, scene))))
            {
                throw new InvalidOperationException("Enabled Build Settings scenes are missing or empty.");
            }

            var output = Path.Combine(projectRoot, "Builds", "WebGL");
            Directory.CreateDirectory(output);

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = output,
                target = BuildTarget.WebGL,
                options = BuildOptions.None
            });

            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException($"WebGL build failed: {report.summary.result}, {report.summary.totalErrors} errors.");
            }

            Debug.Log($"WebGL build succeeded: {output} ({report.summary.totalSize} bytes)");
        }
    }
}
