using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

// Keeps the result callback registered through the Play Mode domain reload.
[InitializeOnLoad]
public static class AlienHuntSmokeRunner
{
    private const string Running = "EchoProtocol.AlienHuntSmokeRunning";
    static AlienHuntSmokeRunner()
    { if (SessionState.GetBool(Running, false)) TestRunnerApi.RegisterTestCallback(new Callback()); }

    [MenuItem("Tools/ECHO Protocol/Test Alien Hunt Offline Cycle")]
    public static void Run() => RunTest("EchoProtocol.Networking.Tests.MapGameplayPlayModeTests.BaitCaptureFuelAndBurnCycleConsumesExactlyOneAlien");
    public static void RunPurgeDirector() => RunTest("EchoProtocol.Networking.Tests.MapGameplayPlayModeTests.DirectorRageUvSupplyAndCatchRunOnStateAuthority");
    private static void RunTest(string name)
    {
        SessionState.SetBool(Running, true);
        TestRunnerApi.RegisterTestCallback(new Callback());
        var api = ScriptableObject.CreateInstance<TestRunnerApi>();
        api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.PlayMode,
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
            string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../.git/alien-hunt-smoke.xml"));
            TestRunnerApi.SaveResultToFile(result, output);
            Debug.Log("[AlienHunt Smoke] " + result.ResultState + ": " + result.Message + "; report=" + output);
        }
    }
}
