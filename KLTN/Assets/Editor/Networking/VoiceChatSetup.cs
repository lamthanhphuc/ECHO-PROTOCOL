using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using UnityEngine.Audio;

namespace EchoProtocol.Editor.Networking
{
    [InitializeOnLoad]
    internal static class VoiceChatSetup
    {
        private const string Request = "Temp/VoiceChatSetup.request";
        static VoiceChatSetup()
        {
            EditorApplication.update += RunRequested;
            if (SessionState.GetBool("Echo.Voice.PlayTests", false))
            {
                var api = ScriptableObject.CreateInstance<TestRunnerApi>();
                api.RegisterCallbacks(new Results(api, true));
            }
        }

        private static void RunRequested()
        {
            if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating
                || EditorApplication.isPlayingOrWillChangePlaymode) return;
            bool play = File.ReadAllText(Request).Trim() == "play";
            File.Delete(Request);
            try { SetupMixer(); if (play) RunStartupTests(); else RunTests(); }
            catch (System.Exception error)
            {
                File.WriteAllText("Temp/VoiceChatSetup.error.txt", error.ToString());
                Debug.LogException(error);
            }
        }

        [MenuItem("ECHO Protocol/Setup/Voice Audio Mixer")]
        public static void SetupMixer()
        {
            const string folder = "Assets/Resources/Voice";
            const string path = folder + "/VoiceMixer.mixer";
            if (AssetDatabase.LoadAssetAtPath<AudioMixer>(path) != null) return;
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new System.InvalidOperationException("Exit Play Mode first.");
            Directory.CreateDirectory(folder);
            AssetDatabase.Refresh();
            // Unity exposes mixer creation only through its internal Editor controller.
            // Resolve the exact signature and fail explicitly if a future Editor changes it.
            var type = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.Audio.AudioMixerController");
            var create = type?.GetMethod("CreateMixerControllerAtPath", BindingFlags.Static | BindingFlags.Public
                | BindingFlags.NonPublic, null, new[] { typeof(string) }, null);
            if (create == null) throw new System.InvalidOperationException("This Unity Editor has no supported mixer creation API.");
            create.Invoke(null, new object[] { path });
            AssetDatabase.SaveAssets();
            var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(path);
            if (mixer == null || mixer.FindMatchingGroups("Master").Length == 0)
                throw new System.InvalidOperationException("Voice mixer creation failed.");
            File.WriteAllText("Temp/VoiceMixerSetup.txt", "PASS: dedicated VoiceMixer and Master output group created.");
        }

        [MenuItem("ECHO Protocol/Tests/Voice Chat Rules")]
        public static void RunTests()
        {
            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            api.RegisterCallbacks(new Results(api));
            api.Execute(new ExecutionSettings(new Filter
            {
                testMode = TestMode.EditMode,
                testNames = new[] { "EchoProtocol.Player.Tests.VoiceChatRulesTests" }
            }) { runSynchronously = true });
        }

        private sealed class Results : ICallbacks
        {
            private readonly TestRunnerApi _api;
            private readonly bool _play;
            public Results(TestRunnerApi api, bool play = false) { _api = api; _play = play; }
            public void RunStarted(ITestAdaptor tests) { }
            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor result) { }
            public void RunFinished(ITestResultAdaptor result)
            {
                string name = _play ? "Temp/VoiceStartupTests" : "Temp/VoiceChatTests";
                TestRunnerApi.SaveResultToFile(result, name + ".xml");
                File.WriteAllText(name + ".txt", $"Passed={result.PassCount}; Failed={result.FailCount}; Skipped={result.SkipCount}");
                if (_play) SessionState.SetBool("Echo.Voice.PlayTests", false);
                _api.UnregisterCallbacks(this);
                Object.DestroyImmediate(_api);
            }
        }

        [MenuItem("ECHO Protocol/Tests/Voice Startup PlayMode")]
        public static void RunStartupTests()
        {
            SessionState.SetBool("Echo.Voice.PlayTests", true);
            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            api.RegisterCallbacks(new Results(api, true));
            api.Execute(new ExecutionSettings(new Filter
            {
                testMode = TestMode.PlayMode,
                assemblyNames = new[] { "EchoProtocol.Voice.PlayMode.Tests" }
            }));
        }
    }
}
