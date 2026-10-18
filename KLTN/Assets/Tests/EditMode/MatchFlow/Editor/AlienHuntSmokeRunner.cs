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
    public static void Run()
    {
        SessionState.SetBool(Running, true);
        SessionState.SetBool(Running + ".OptionsEnabled", EditorSettings.enterPlayModeOptionsEnabled);
        SessionState.SetInt(Running + ".Options", (int)EditorSettings.enterPlayModeOptions);
        EditorSettings.enterPlayModeOptionsEnabled = true;
        EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
        TestRunnerApi.RegisterTestCallback(new Callback());
        var api = ScriptableObject.CreateInstance<TestRunnerApi>();
        api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode,
            testNames = new[] { "AlienHuntSceneTests.BaitCaptureFuelAndBurnCycleConsumesExactlyOneAlien" } }));
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
