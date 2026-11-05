using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EchoProtocol.AI.Common.AED;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace EchoProtocol.Editor.AED
{
    public sealed class AEDTestLabWindow : EditorWindow
    {
        [SerializeField] private AEDTestLabScenario _scenario = new AEDTestLabScenario();
        [SerializeField] private List<AEDTestLabResult> _history = new List<AEDTestLabResult>();
        private Vector2 _scroll;
        private int _preset;
        private string _error;
        private bool _editPlan;
        [SerializeField] private bool _batch;
        private static readonly string[] Channels = { "Stalker (30%)", "Minion (20%)", "Down (25%)", "Resource (15%)", "Objective (10%)" };

        [MenuItem("Tools/Echo Protocol/AED/Test Lab")]
        public static void Open()
        {
            var window = GetWindow<AEDTestLabWindow>("Adaptive AI Test Lab");
            window.minSize = new Vector2(720, 560);
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("ADAPTIVE AI / TEST LAB", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("LOCAL SIMULATION • No Play mode needed. Runs existing relief policy and plan validator. No Backend approval, scene/NavMesh validation, Fusion replication, world effect or receipt is claimed. Manual proposals validate plan constraints only; history, cooldowns and resource placement still need Backend/runtime tests.", MessageType.Info);
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            { EditorGUILayout.HelpBox("Exit Play mode to use the offline Test Lab.", MessageType.Warning); return; }
            if (!string.IsNullOrEmpty(_error)) EditorGUILayout.HelpBox(_error, MessageType.Error);
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            EditorGUILayout.BeginHorizontal();
            _preset = EditorGUILayout.Popup("Scenario preset", _preset, AEDTestLabPresets.Names);
            if (GUILayout.Button("Load / Reset", GUILayout.Width(110))) LoadPreset(_preset);
            if (GUILayout.Button("Import JSON", GUILayout.Width(105))) Import();
            if (GUILayout.Button("Save JSON", GUILayout.Width(95))) SaveScenario();
            EditorGUILayout.EndHorizontal();
            EditorGUI.BeginChangeCheck();
            _scenario.name = EditorGUILayout.TextField("Scenario name", _scenario.name);
            _scenario.action = (LabAction)EditorGUILayout.EnumPopup("Action", _scenario.action);
            _scenario.adaptive = EditorGUILayout.Toggle("Adaptive (off = Fixed)", _scenario.adaptive);
            _scenario.completeEvidence = EditorGUILayout.Toggle("Complete synthetic evidence", _scenario.completeEvidence);
            _scenario.evidenceAgeSeconds = EditorGUILayout.DoubleField("Evidence age / seconds", _scenario.evidenceAgeSeconds);
            _scenario.safeWindow = EditorGUILayout.Toggle("Safe window", _scenario.safeWindow);
            _scenario.recentDown = EditorGUILayout.Toggle("Recent Down", _scenario.recentDown);
            _scenario.finalPhase = EditorGUILayout.Toggle("Final Hunt / Escape", _scenario.finalPhase);
            _scenario.zone = EditorGUILayout.IntSlider("Zone", _scenario.zone, 1, 3);
            _scenario.decisionPoint = (AEDV02DecisionPoint)EditorGUILayout.EnumPopup("Decision point", _scenario.decisionPoint);
            _scenario.initialOptionalBudget = EditorGUILayout.IntSlider("Initial optional budget", _scenario.initialOptionalBudget, 0, 2);
            _scenario.liveZone3Minions = EditorGUILayout.IntField("Live Zone 3 Minions", _scenario.liveZone3Minions);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Synthetic pressure channels", EditorStyles.boldLabel);
            for (int i = 0; i < 5; i++) _scenario.pressure[i] = EditorGUILayout.Slider(Channels[i], (float)_scenario.pressure[i], 0, 1);
            var score = _scenario.pressure.Select((x, i) => x * new[] { .30, .20, .25, .15, .10 }[i]).Sum();
            bool known = _scenario.completeEvidence && _scenario.evidenceAgeSeconds >= 0 && _scenario.evidenceAgeSeconds <= AEDV02PilotPolicy.EvidenceMaxAgeSeconds;
            int tier = known ? AEDV02PilotPolicy.Tier(score) : -1;
            EditorGUILayout.LabelField($"Weighted PP: {score:F3}    Tier: {(known ? "PP" + tier : "UNKNOWN")}    Target adjustment: {AEDV02PilotPolicy.TargetAdjustment(tier):+0.00;-0.00;0.00}    Sensory multiplier: {AEDV02PilotPolicy.Sensory(tier):F2}");
            _editPlan = EditorGUILayout.Foldout(_editPlan, "Edit current / manual proposed plan (whitelisted values)", true);
            if (_editPlan)
            {
                EditorGUILayout.HelpBox("Changing a manual plan does not create a policy recommendation. Resource/Minion proposals here are local contract probes.", MessageType.None);
                foreach (AEDV02Key key in Enum.GetValues(typeof(AEDV02Key)))
                {
                    int i = (int)key;
                    var values = AEDV02Plan.AllowedValues(key).ToArray();
                    var labels = values.Select(x => x.ToString(System.Globalization.CultureInfo.InvariantCulture)).ToArray();
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(key.ToString(), GUILayout.Width(235));
                    _scenario.before[i] = values[EditorGUILayout.Popup(Array.IndexOf(values, _scenario.before[i]), labels)];
                    using (new EditorGUI.DisabledScope(_scenario.action != LabAction.ValidateManualPlan))
                        _scenario.proposed[i] = values[EditorGUILayout.Popup(Array.IndexOf(values, _scenario.proposed[i]), labels)];
                    EditorGUILayout.EndHorizontal();
                }
            }
            if (EditorGUI.EndChangeCheck()) _error = null;
            EditorGUILayout.Space();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Evaluate → Next Step", GUILayout.Height(30))) EvaluateStep();
            if (GUILayout.Button("Run All Presets", GUILayout.Height(30))) RunAllPresets();
            if (GUILayout.Button("Export Results", GUILayout.Height(30))) Export();
            if (GUILayout.Button("Run EditMode Checks", GUILayout.Height(30))) RunTests();
            EditorGUILayout.EndHorizontal();
            if (_history.Count > 0)
            {
                if (_batch) EditorGUILayout.HelpBox("Independent preset batch. Each row starts from its own preset, not the previous row. Evaluate starts a new history using the editable scenario above.", MessageType.Info);
                var last = _history[_history.Count - 1];
                EditorGUILayout.Space();
                EditorGUILayout.HelpBox($"Step {_history.Count}: {last.scenario}\n{last.reason}\n{(last.accepted ? "Locally accepted; next step starts from After." : "Held; current plan unchanged.")}", last.accepted ? MessageType.Info : MessageType.Warning);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Parameter", EditorStyles.boldLabel, GUILayout.Width(235));
                foreach (string title in new[] { "Before", "Proposed", "After", "Delta" }) EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
                EditorGUILayout.EndHorizontal();
                foreach (AEDV02Key key in Enum.GetValues(typeof(AEDV02Key)))
                {
                    int i = (int)key;
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(key.ToString(), GUILayout.Width(235));
                    EditorGUILayout.LabelField(last.before[i].ToString("0.###"));
                    EditorGUILayout.LabelField(last.proposed[i].ToString("0.###"));
                    EditorGUILayout.LabelField(last.after[i].ToString("0.###"));
                    EditorGUILayout.LabelField((last.after[i] - last.before[i]).ToString("+0.###;-0.###;0"));
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUILayout.LabelField("Step history (latest first)", EditorStyles.boldLabel);
                for (int i = _history.Count - 1; i >= 0; i--)
                    EditorGUILayout.LabelField($"{i + 1}. {_history[i].scenario} — {_history[i].reason}", EditorStyles.wordWrappedLabel);
            }
            EditorGUILayout.EndScrollView();
        }

        public void LoadPreset(int index)
        { _scenario = AEDTestLabPresets.Create(index); _history.Clear(); _batch = false; _error = null; Repaint(); }

        public void EvaluateStep()
        {
            try
            {
                var result = AEDTestLabEvaluator.Evaluate(_scenario);
                if (_batch) { _history.Clear(); _batch = false; }
                _history.Add(result);
                if (_history.Count > 200) _history.RemoveAt(0);
                if (result.accepted) _scenario.before = (double[])result.after.Clone();
                _error = null;
            }
            catch (ArgumentException ex) { _error = ex.Message; }
            Repaint();
        }

        public void RunAllPresets()
        {
            _history.Clear(); _batch = true; _error = null;
            for (int i = 0; i < AEDTestLabPresets.Names.Length; i++)
                _history.Add(AEDTestLabEvaluator.Evaluate(AEDTestLabPresets.Create(i)));
            Repaint();
        }

        private void Import()
        {
            var path = EditorUtility.OpenFilePanel("Import synthetic AED scenario", "", "json");
            if (string.IsNullOrEmpty(path)) return;
            try
            {
                var scenario = JsonUtility.FromJson<AEDTestLabScenario>(File.ReadAllText(path));
                AEDTestLabEvaluator.Validate(scenario);
                _scenario = scenario; _history.Clear(); _batch = false; _error = null;
            }
            catch (Exception ex) when (ex is ArgumentException || ex is IOException || ex is UnauthorizedAccessException)
            { _error = ex.Message; }
        }

        private void SaveScenario()
        {
            try
            {
                AEDTestLabEvaluator.Validate(_scenario);
                Save("Save synthetic scenario", "aed-scenario.json", JsonUtility.ToJson(_scenario, true));
            }
            catch (ArgumentException ex) { _error = ex.Message; }
        }

        [Serializable] private sealed class Report
        {
            public string scope = "LOCAL SIMULATION ONLY; not E2E evidence";
            public string policy = AEDV02PilotPolicy.Version, catalog = AEDV02Plan.CatalogVersion;
            public bool independentPresetBatch;
            public List<AEDTestLabResult> steps;
        }

        private void Export() => Save("Export local simulation results", "aed-lab-results.json", JsonUtility.ToJson(new Report { steps = _history, independentPresetBatch = _batch }, true));
        private void Save(string title, string filename, string json)
        {
            var path = EditorUtility.SaveFilePanel(title, "", filename, "json");
            if (string.IsNullOrEmpty(path)) return;
            try { File.WriteAllText(path, json); _error = null; }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException) { _error = ex.Message; }
        }

        [MenuItem("Tools/Echo Protocol/AED/Run Local EditMode Checks")]
        public static void RunTests()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
            var api = CreateInstance<TestRunnerApi>();
            api.RegisterCallbacks(new Results(api));
            api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode,
                testNames = new[] { "EchoProtocol.Editor.AED.AEDTestLabTests", "EchoProtocol.AI.Common.Tests.AEDMinionDirectorV1Tests", "AEDV02PlanTests", "AEDV02PilotPolicyTests", "AEDPilotEndpointTests", "EchoProtocol.Networking.Tests.AEDV02HostGuardTests" }
            }) { runSynchronously = true });
        }

        private sealed class Results : ICallbacks
        {
            private readonly TestRunnerApi _api;
            private readonly string _path;
            public Results(TestRunnerApi api, string path = "Temp/AEDTestLab.Tests.xml") { _api = api; _path = path; }
            public void RunStarted(ITestAdaptor tests) { }
            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor result) { }
            public void RunFinished(ITestResultAdaptor result)
            {
                TestRunnerApi.SaveResultToFile(result, _path);
                Debug.Log($"[AED_TEST_LAB] EditMode: passed={result.PassCount}, failed={result.FailCount}, skipped={result.SkipCount}; {_path}");
                _api.UnregisterCallbacks(this); DestroyImmediate(_api);
            }
        }

        [MenuItem("Tools/Echo Protocol/AED/Run Common Regression")]
        public static void RunCommonRegression()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
            var api = CreateInstance<TestRunnerApi>();
            api.RegisterCallbacks(new Results(api, "Temp/AEDTestLab.Common.Tests.xml"));
            api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode,
                assemblyNames = new[] { "EchoProtocol.AI.Common.EditMode.Tests" }
            }) { runSynchronously = true });
        }
    }
}
