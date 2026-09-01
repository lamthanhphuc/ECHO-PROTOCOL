using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace EchoProtocol.Telemetry.Editor
{
    /// <summary>
    /// Keeps the canonical telemetry acceptance suite runnable from an already-open Editor.
    /// </summary>
    public static class TelemetryEditModeTestMenu
    {
        private const string TestAssembly = "EchoProtocol.Telemetry.EditMode.Tests";

        [MenuItem("ECHO PROTOCOL/Tests/Run Telemetry EditMode")]
        public static void Run()
        {
            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            var callbacks = new ResultCallbacks(api);
            api.RegisterCallbacks(callbacks);

            var settings = new ExecutionSettings(new Filter
            {
                testMode = TestMode.EditMode,
                assemblyNames = new[] { TestAssembly }
            })
            {
                runSynchronously = true
            };

            Debug.Log($"TEL-TESTS|result=STARTED|assembly={TestAssembly}");
            api.Execute(settings);
        }

        private sealed class ResultCallbacks : ICallbacks
        {
            private readonly TestRunnerApi _api;

            public ResultCallbacks(TestRunnerApi api)
            {
                _api = api;
            }

            public void RunStarted(ITestAdaptor testsToRun)
            {
            }

            public void RunFinished(ITestResultAdaptor result)
            {
                var status = result.FailCount == 0 && result.PassCount > 0 ? "PASS" : "FAIL";
                Debug.Log(
                    $"TEL-TESTS|result={status}|passed={result.PassCount}|failed={result.FailCount}|" +
                    $"skipped={result.SkipCount}|inconclusive={result.InconclusiveCount}|duration={result.Duration:F3}s");

                _api.UnregisterCallbacks(this);
                Object.DestroyImmediate(_api);
            }

            public void TestStarted(ITestAdaptor test)
            {
            }

            public void TestFinished(ITestResultAdaptor result)
            {
                if (result.HasChildren || result.TestStatus.ToString() != "Failed")
                {
                    return;
                }

                Debug.LogError($"TEL-TESTS|test=FAIL|name={result.FullName}|message={result.Message}");
            }
        }
    }
}
