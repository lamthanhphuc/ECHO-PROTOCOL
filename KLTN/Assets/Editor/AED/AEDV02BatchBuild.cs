using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class AEDV02BatchBuild
{
    [Serializable] private sealed class Result
    {
        public string result, unity, output, candidate;
        public int errors, warnings;
        public ulong bytes;
        public double seconds;
    }

    public static void ExportManifest()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/SciFi.unity", OpenSceneMode.Single);
        Debug.Log(ExportAEDV02Scene.Run());
    }

    public static void WindowsDevelopment()
    {
        string output = Argument("-aedBuildOutput");
        string candidate = Argument("-aedCandidate");
        if (string.IsNullOrWhiteSpace(output) || string.IsNullOrWhiteSpace(candidate))
            throw new ArgumentException("Build requires -aedBuildOutput and -aedCandidate.");
        output = Path.GetFullPath(output);
        var scenes = EditorBuildSettings.scenes.Where(x => x.enabled).Select(x => x.path).ToArray();
        if (scenes.Length == 0 || scenes.Any(x => !File.Exists(x))) throw new InvalidOperationException("Build scenes missing.");
        Directory.CreateDirectory(Path.GetDirectoryName(output));
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = scenes, locationPathName = output, target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.Development,
            extraScriptingDefines = new[] { "AED_V02_PILOT_HARNESS" }
        });
        var summary = report.summary;
        File.WriteAllText(Path.Combine(Path.GetDirectoryName(output), "build-report.json"), JsonUtility.ToJson(new Result
        {
            result = summary.result.ToString(), unity = Application.unityVersion, output = output, candidate = candidate,
            errors = (int)summary.totalErrors, warnings = (int)summary.totalWarnings, bytes = summary.totalSize,
            seconds = summary.totalTime.TotalSeconds
        }, true));
        if (summary.result != BuildResult.Succeeded || summary.totalErrors != 0 || !File.Exists(output))
            throw new InvalidOperationException("Windows Development build failed: " + summary.result);
    }

    private static string Argument(string name)
    {
        var args = Environment.GetCommandLineArgs();
        int index = Array.IndexOf(args, name);
        return index < 0 || index + 1 >= args.Length ? null : args[index + 1];
    }
}
