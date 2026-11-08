using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

// Keeps the result callback registered through the smoke test's Play mode reloads.
[InitializeOnLoad]
public static class AlienHuntSmokeRunner
{
    private const string Running = "EchoProtocol.AlienHuntSmokeRunning";
    static AlienHuntSmokeRunner()
    { if (SessionState.GetBool(Running, false)) TestRunnerApi.RegisterTestCallback(new Callback()); }

    [MenuItem("Tools/ECHO Protocol/Test Alien Hunt Offline Cycle")]
    public static void Run() => RunTest("AlienHuntSceneTests.BaitCaptureFuelAndBurnCycleConsumesExactlyOneAlien");
    public static void RunPurgeDirector() => RunTest("PurgeDirectorTests.DirectorRageUvSupplyAndCatchRunOnStateAuthority");
    private static void RunTest(string name)
    {
        SessionState.SetBool(Running, true);
        SessionState.SetBool(Running + ".OptionsEnabled", EditorSettings.enterPlayModeOptionsEnabled);
        SessionState.SetInt(Running + ".Options", (int)EditorSettings.enterPlayModeOptions);
        EditorSettings.enterPlayModeOptionsEnabled = true;
        EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
        TestRunnerApi.RegisterTestCallback(new Callback());
        var api = ScriptableObject.CreateInstance<TestRunnerApi>();
        api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode,
            testNames = new[] { name } }));
    }

    private sealed class Callback : ICallbacks
    {
        public void RunStarted(ITestAdaptor testsToRun) { }
        public void TestStarted(ITestAdaptor test) { }
        public void TestFinished(ITestResultAdaptor result) { }
        public void RunFinished(ITestResultAdaptor result)
        {
            SessionState.SetBool(Running, false);
            EditorSettings.enterPlayModeOptions = (EnterPlayModeOptions)SessionState.GetInt(Running + ".Options", 0);
            EditorSettings.enterPlayModeOptionsEnabled = SessionState.GetBool(Running + ".OptionsEnabled", false);
            string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../.git/alien-hunt-smoke.xml"));
            TestRunnerApi.SaveResultToFile(result, output);
            Debug.Log("[AlienHunt Smoke] " + result.ResultState + ": " + result.Message + "; report=" + output);
        }
    }
}

// These EditMode coroutines keep local state across the Play Mode transition.
internal sealed class MatchFlowPlayModeSettings
{
    private readonly bool wasEnabled = EditorSettings.enterPlayModeOptionsEnabled;
    private readonly EnterPlayModeOptions previousOptions = EditorSettings.enterPlayModeOptions;
    private readonly ScriptableObject editModeRunner;
    private readonly HideFlags runnerHideFlags;

    public MatchFlowPlayModeSettings()
    {
        // TestJobData is managed state. Keep its native runner alive during scene asset unloading.
        foreach (var candidate in Resources.FindObjectsOfTypeAll<ScriptableObject>())
        {
            if (candidate.GetType().FullName != "UnityEditor.TestTools.TestRunner.EditModeRunner")
                continue;

            editModeRunner = candidate;
            runnerHideFlags = candidate.hideFlags;
            candidate.hideFlags |= HideFlags.DontUnloadUnusedAsset;
            break;
        }

        EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
        EditorSettings.enterPlayModeOptionsEnabled = true;
    }

    public void Restore()
    {
        if (editModeRunner) editModeRunner.hideFlags = runnerHideFlags;
        EditorSettings.enterPlayModeOptions = previousOptions;
        EditorSettings.enterPlayModeOptionsEnabled = wasEnabled;
    }
}
