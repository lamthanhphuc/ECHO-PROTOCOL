using System.IO;
using EchoProtocol.RelayA;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class RelayASetupBuilder
{
    private const string RelayAPrefabPath = "Assets/import/RelayA/RelayA.prefab";
    private const string ConfigPath = "Assets/ScriptableObjects/RelayA/RelayA_Hard_Config.asset";

    [MenuItem("Tools/ECHO Protocol/Setup Relay A Voltage Stabilization")]
    public static void SetupRelayA()
    {
        EnsureFolder("Assets/ScriptableObjects/RelayA");
        RelayAConfig config = AssetDatabase.LoadAssetAtPath<RelayAConfig>(ConfigPath);
        if (config == null)
        {
            config = ScriptableObject.CreateInstance<RelayAConfig>();
            AssetDatabase.CreateAsset(config, ConfigPath);
        }

        GameObject root = PrefabUtility.LoadPrefabContents(RelayAPrefabPath);
        try
        {
            BoxCollider boxCollider = EnsureComponent<BoxCollider>(root);
            boxCollider.isTrigger = false;
            boxCollider.center = new Vector3(1.60f, 0.90f, 1.75f);
            boxCollider.size = new Vector3(1.60f, 1.80f, 2.20f);

            RelayAController controller = EnsureComponent<RelayAController>(root);
            RelayAInteraction interaction = EnsureComponent<RelayAInteraction>(root);
            AudioSource audio = EnsureComponent<AudioSource>(root);
            audio.playOnAwake = false;
            audio.spatialBlend = 1f;
            audio.rolloffMode = AudioRolloffMode.Linear;
            audio.maxDistance = 16f;

            RelayAUIController ui = BuildUi(root.transform);

            SerializedObject controllerSo = new SerializedObject(controller);
            SetObject(controllerSo, "config", config);
            SetObject(controllerSo, "ui", ui);
            SetObject(controllerSo, "audioSource", audio);
            controllerSo.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject interactionSo = new SerializedObject(interaction);
            SetObject(interactionSo, "controller", controller);
            interactionSo.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, RelayAPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[RelayASetupBuilder] Relay A Voltage Stabilization UI and gameplay references are ready.");
    }

    [MenuItem("Tools/ECHO Protocol/Run Relay A EditMode Tests")]
    public static void RunRelayAEditModeTests()
    {
        int passed = 0;
        int failed = 0;
        var failures = new System.Text.StringBuilder();

        RunSelfTest("Voltage_OutOfSafeRange_EvaluatesUnstable", TestVoltageOutOfSafeRange, ref passed, ref failed, failures);
        RunSelfTest("Frequency_OutOfSafeRange_EvaluatesUnstable", TestFrequencyOutOfSafeRange, ref passed, ref failed, failures);
        RunSelfTest("LoadBalance_45Point7Percent_EvaluatesLowLoadUnstable", TestLoadBalance45Point7PercentEvaluatesLowLoadUnstable, ref passed, ref failed, failures);
        RunSelfTest("StabilityTimer_TicksOnlyWhenAllThreeMetricsSafe", TestStabilityTimerTicksOnlyWhenAllThreeMetricsSafe, ref passed, ref failed, failures);
        RunSelfTest("ProgressBar_ZeroSeconds_FillsZeroPercent", TestProgressBarZeroSecondsFillsZeroPercent, ref passed, ref failed, failures);
        RunSelfTest("ProgressBar_SixSeconds_FillsFiftyPercent", TestProgressBarSixSecondsFillsFiftyPercent, ref passed, ref failed, failures);
        RunSelfTest("ProgressBar_TwelveSeconds_FillsHundredPercent", TestProgressBarTwelveSecondsFillsHundredPercent, ref passed, ref failed, failures);
        RunSelfTest("Controls_HaveCrossCoupledEffectsOnOutputs", TestControlsCrossCoupling, ref passed, ref failed, failures);
        RunSelfTest("Outputs_ApproachTargetsWithResponseDelay", TestResponseDelay, ref passed, ref failed, failures);
        RunSelfTest("HardConfig_HasSolvableControlSet", TestHardConfigHasSolution, ref passed, ref failed, failures);
        RunSelfTest("StabilityTimer_HandlesGracePeriodAndResetsWithoutAlteringSliders", TestStabilityTimerGracePeriodAndReset, ref passed, ref failed, failures);
        RunSelfTest("EmergencyStop_PreservesFaultStateWithoutBypassing", TestEmergencyStopPreservesFaultState, ref passed, ref failed, failures);
        RunSelfTest("ElectricalFault_IsSolvableAndDoesNotLoopInfinitely", TestElectricalFaultSolvableAndNonLooping, ref passed, ref failed, failures);
        RunSelfTest("RelayA_DoesNotGoOnlineBeforeTwelveContinuousValidSeconds", TestDoesNotGoOnlineBeforeTwelveSeconds, ref passed, ref failed, failures);
        RunSelfTest("OnlineState_LocksInteractiveControls", TestOnlineStateLocksControls, ref passed, ref failed, failures);

        string status = failed == 0 && passed > 0 ? "PASS" : "FAIL";
        string summary = $"status={status} passed={passed} failed={failed} skipped=0";
        string logFolder = Path.Combine(Application.dataPath, "../Logs");
        if (!Directory.Exists(logFolder))
        {
            Directory.CreateDirectory(logFolder);
        }
        string outputPath = Path.Combine(logFolder, "RelayAEditModeTestResult.txt");
        File.WriteAllText(outputPath, summary + "\n" + failures);
        if (failed == 0)
        {
            Debug.Log("[RelayA-TESTS] Finished: " + summary);
        }
        else
        {
            Debug.LogError("[RelayA-TESTS] Finished: " + summary + "\n" + failures);
        }
    }

    private static void RunSelfTest(string name, System.Action test, ref int passed, ref int failed, System.Text.StringBuilder failures)
    {
        try
        {
            test();
            passed++;
        }
        catch (System.Exception ex)
        {
            failed++;
            failures.AppendLine($"FAILED: {name} -> {ex.Message}");
        }
    }

    private static void TestVoltageOutOfSafeRange()
    {
        RelayAConfig config = ScriptableObject.CreateInstance<RelayAConfig>();
        try
        {
            var outLow = new RelayAOutputs(215f, 50f, 50f);
            if (config.IsVoltageSafe(outLow.Voltage))
                throw new System.InvalidOperationException("215V evaluated as safe voltage.");
            if (config.IsOutputStable(outLow))
                throw new System.InvalidOperationException("Output with 215V evaluated as stable.");

            var outHigh = new RelayAOutputs(235f, 50f, 50f);
            if (config.IsVoltageSafe(outHigh.Voltage))
                throw new System.InvalidOperationException("235V evaluated as safe voltage.");
            if (config.IsOutputStable(outHigh))
                throw new System.InvalidOperationException("Output with 235V evaluated as stable.");
        }
        finally
        {
            Object.DestroyImmediate(config);
        }
    }

    private static void TestFrequencyOutOfSafeRange()
    {
        RelayAConfig config = ScriptableObject.CreateInstance<RelayAConfig>();
        try
        {
            var outLow = new RelayAOutputs(225f, 48f, 50f);
            if (config.IsFrequencySafe(outLow.Frequency))
                throw new System.InvalidOperationException("48Hz evaluated as safe frequency.");
            if (config.IsOutputStable(outLow))
                throw new System.InvalidOperationException("Output with 48Hz evaluated as stable.");

            var outHigh = new RelayAOutputs(225f, 52f, 50f);
            if (config.IsFrequencySafe(outHigh.Frequency))
                throw new System.InvalidOperationException("52Hz evaluated as safe frequency.");
            if (config.IsOutputStable(outHigh))
                throw new System.InvalidOperationException("Output with 52Hz evaluated as stable.");
        }
        finally
        {
            Object.DestroyImmediate(config);
        }
    }

    private static void TestLoadBalance45Point7PercentEvaluatesLowLoadUnstable()
    {
        RelayAConfig config = ScriptableObject.CreateInstance<RelayAConfig>();
        try
        {
            float testLoad = 45.7f;
            if (config.IsLoadSafe(testLoad))
                throw new System.InvalidOperationException("Load Balance 45.7% evaluated as safe (safe range 47-53%).");

            var outputs = new RelayAOutputs(225f, 50f, testLoad);
            if (config.IsOutputStable(outputs))
                throw new System.InvalidOperationException("Outputs with Load Balance 45.7% evaluated as stable.");
        }
        finally
        {
            Object.DestroyImmediate(config);
        }
    }

    private static void TestStabilityTimerTicksOnlyWhenAllThreeMetricsSafe()
    {
        RelayAConfig config = ScriptableObject.CreateInstance<RelayAConfig>();
        try
        {
            RelayASimulation sim = new RelayASimulation();
            sim.Initialize(config);
            sim.Start();

            sim.SetControls(0f, 0f, 0f);
            for (int i = 0; i < 20; i++) sim.Tick(0.1f);

            if (sim.Snapshot.IsStable)
                throw new System.InvalidOperationException("Controls (0,0,0) produced stable output unexpectedly.");

            if (sim.Snapshot.StabilitySeconds > 0f)
                throw new System.InvalidOperationException($"Stability timer ticked while unstable: {sim.Snapshot.StabilitySeconds}s");

            Vector3 solved = config.SolvedControls;
            sim.SetControls(solved.x, solved.y, solved.z);

            for (int i = 0; i < 40 && !sim.Snapshot.IsStable; i++) sim.Tick(0.1f);

            if (!sim.Snapshot.IsStable)
                throw new System.InvalidOperationException("Solved controls failed to reach stable state.");

            float timerBefore = sim.Snapshot.StabilitySeconds;
            sim.Tick(0.5f);
            float timerAfter = sim.Snapshot.StabilitySeconds;

            if (timerAfter <= timerBefore)
                throw new System.InvalidOperationException("Stability timer did not advance while all 3 metrics are safe.");
        }
        finally
        {
            Object.DestroyImmediate(config);
        }
    }

    private static void TestProgressBarZeroSecondsFillsZeroPercent()
    {
        RelayAConfig config = ScriptableObject.CreateInstance<RelayAConfig>();
        try
        {
            RelayASimulation sim = new RelayASimulation();
            sim.Initialize(config);
            if (sim.Snapshot.Stability01 != 0f)
            {
                throw new System.InvalidOperationException($"Expected Stability01 = 0, got {sim.Snapshot.Stability01}");
            }
        }
        finally
        {
            Object.DestroyImmediate(config);
        }
    }

    private static void TestProgressBarSixSecondsFillsFiftyPercent()
    {
        RelayAConfig config = ScriptableObject.CreateInstance<RelayAConfig>();
        try
        {
            RelayASimulation sim = new RelayASimulation();
            sim.Initialize(config);
            Vector3 solved = config.SolvedControls;
            sim.SetControls(solved.x, solved.y, solved.z);
            sim.Start();

            for (int i = 0; i < 40 && !sim.Snapshot.IsStable; i++) sim.Tick(0.1f);

            while (sim.Snapshot.StabilitySeconds < 6.0f)
            {
                float step = Mathf.Min(0.1f, 6.0f - sim.Snapshot.StabilitySeconds);
                sim.Tick(step);
            }

            float ratio = sim.Snapshot.Stability01;
            if (Mathf.Abs(ratio - 0.5f) > 0.02f)
            {
                throw new System.InvalidOperationException($"Expected ~50% fill at 6s/12s, got {ratio * 100f}%");
            }
        }
        finally
        {
            Object.DestroyImmediate(config);
        }
    }

    private static void TestProgressBarTwelveSecondsFillsHundredPercent()
    {
        RelayAConfig config = ScriptableObject.CreateInstance<RelayAConfig>();
        try
        {
            RelayASimulation sim = new RelayASimulation();
            sim.Initialize(config);
            Vector3 solved = config.SolvedControls;
            sim.SetControls(solved.x, solved.y, solved.z);
            sim.Start();

            for (int i = 0; i < 300 && !sim.Snapshot.IsOnline; i++)
            {
                sim.Tick(0.1f);
            }

            if (!sim.Snapshot.IsOnline)
            {
                throw new System.InvalidOperationException("Relay A failed to complete 12s.");
            }

            float ratio = sim.Snapshot.Stability01;
            if (Mathf.Abs(ratio - 1.0f) > 0.01f)
            {
                throw new System.InvalidOperationException($"Expected 100% fill at 12s/12s, got {ratio * 100f}%");
            }
        }
        finally
        {
            Object.DestroyImmediate(config);
        }
    }

    private static void TestControlsCrossCoupling()
    {
        RelayAConfig config = ScriptableObject.CreateInstance<RelayAConfig>();
        try
        {
            var baseOutputs = config.EvaluateTarget(new Vector3(50f, 50f, 50f), RelayAFaultType.None, 0f);

            var modGen = config.EvaluateTarget(new Vector3(70f, 50f, 50f), RelayAFaultType.None, 0f);
            if (Mathf.Abs(modGen.Voltage - baseOutputs.Voltage) < 1f)
                throw new System.InvalidOperationException("Generator output did not significantly affect voltage.");

            var modFreq = config.EvaluateTarget(new Vector3(50f, 70f, 50f), RelayAFaultType.None, 0f);
            if (Mathf.Abs(modFreq.Frequency - baseOutputs.Frequency) < 0.5f)
                throw new System.InvalidOperationException("Frequency regulator did not significantly affect frequency.");

            var modLoad = config.EvaluateTarget(new Vector3(50f, 50f, 70f), RelayAFaultType.None, 0f);
            if (Mathf.Abs(modLoad.LoadBalance - baseOutputs.LoadBalance) < 1f)
                throw new System.InvalidOperationException("Load distribution did not significantly affect load balance.");
        }
        finally
        {
            Object.DestroyImmediate(config);
        }
    }

    private static void TestHardConfigHasSolution()
    {
        RelayAConfig config = ScriptableObject.CreateInstance<RelayAConfig>();
        try
        {
            if (!config.HasValidSolution(out RelayAOutputs outputs))
            {
                throw new System.InvalidOperationException("SolvedControls do not produce safe outputs.");
            }

            AssertInRange(outputs.Voltage, config.VoltageSafeRange, "Voltage");
            AssertInRange(outputs.Frequency, config.FrequencySafeRange, "Frequency");
            AssertInRange(outputs.LoadBalance, config.LoadSafeRange, "LoadBalance");
        }
        finally
        {
            Object.DestroyImmediate(config);
        }
    }

    private static void TestResponseDelay()
    {
        RelayAConfig config = ScriptableObject.CreateInstance<RelayAConfig>();
        try
        {
            RelayASimulation simulation = new RelayASimulation();
            simulation.Initialize(config);
            RelayAOutputs initial = simulation.Snapshot.Outputs;
            Vector3 solved = config.SolvedControls;
            simulation.SetControls(solved.x, solved.y, solved.z);
            RelayAOutputs target = simulation.Snapshot.TargetOutputs;
            simulation.Tick(0.1f);
            RelayAOutputs afterShortTick = simulation.Snapshot.Outputs;

            if (Mathf.Abs(afterShortTick.Voltage - initial.Voltage) <= 0f)
            {
                throw new System.InvalidOperationException("Voltage did not begin moving toward target.");
            }

            if (Mathf.Abs(afterShortTick.Voltage - target.Voltage) <= 0.05f)
            {
                throw new System.InvalidOperationException("Voltage snapped to target without visible delay.");
            }

            for (int i = 0; i < 30; i++)
            {
                simulation.Tick(0.1f);
            }

            if (Mathf.Abs(simulation.Snapshot.Outputs.Voltage - target.Voltage) >= Mathf.Abs(afterShortTick.Voltage - target.Voltage))
            {
                throw new System.InvalidOperationException("Voltage did not continue approaching target.");
            }
        }
        finally
        {
            Object.DestroyImmediate(config);
        }
    }

    private static void TestStabilityTimerGracePeriodAndReset()
    {
        RelayAConfig config = ScriptableObject.CreateInstance<RelayAConfig>();
        try
        {
            RelayASimulation sim = new RelayASimulation();
            sim.Initialize(config);
            Vector3 solved = config.SolvedControls;
            sim.SetControls(solved.x, solved.y, solved.z);
            sim.Start();

            for (int i = 0; i < 40 && !sim.Snapshot.IsStable; i++) sim.Tick(0.1f);
            while (sim.Snapshot.StabilitySeconds < 3.0f) sim.Tick(0.1f);

            float accumulated = sim.Snapshot.StabilitySeconds;
            if (accumulated < 3.0f)
                throw new System.InvalidOperationException("Failed to accumulate 3.0s stability.");

            sim.SetControls(0f, 0f, 0f);
            sim.Tick(0.3f);

            if (sim.Snapshot.StabilitySeconds <= 0f)
                throw new System.InvalidOperationException("Timer prematurely reset within 0.3s grace period.");

            sim.SetControls(solved.x, solved.y, solved.z);
            for (int i = 0; i < 30 && !sim.Snapshot.IsStable; i++) sim.Tick(0.1f);

            sim.SetControls(0f, 0f, 0f);
            sim.Tick(0.8f);

            if (sim.Snapshot.StabilitySeconds > 0.01f)
                throw new System.InvalidOperationException("Timer failed to reset after exceeding 0.5s grace period.");

            if (sim.Snapshot.Controls != new Vector3(0f, 0f, 0f))
                throw new System.InvalidOperationException("Player controls were modified/reset when stability timer reset.");
        }
        finally
        {
            Object.DestroyImmediate(config);
        }
    }

    private static void TestEmergencyStopPreservesFaultState()
    {
        RelayAConfig config = ScriptableObject.CreateInstance<RelayAConfig>();
        try
        {
            RelayASimulation sim = new RelayASimulation();
            sim.Initialize(config);
            sim.Start();

            for (int i = 0; i < 150 && sim.Snapshot.WarningFault == RelayAFaultType.None && sim.Snapshot.ActiveFault == RelayAFaultType.None; i++)
            {
                sim.Tick(0.1f);
            }

            RelayAFaultType faultBefore = sim.Snapshot.WarningFault != RelayAFaultType.None ? sim.Snapshot.WarningFault : sim.Snapshot.ActiveFault;
            if (faultBefore == RelayAFaultType.None)
            {
                throw new System.InvalidOperationException("Fault did not trigger as scheduled.");
            }

            sim.EmergencyStop();

            if (sim.Snapshot.IsRunning)
                throw new System.InvalidOperationException("EmergencyStop failed to stop running.");

            if (sim.Snapshot.StabilitySeconds != 0f)
                throw new System.InvalidOperationException("EmergencyStop failed to reset stability seconds.");

            RelayAFaultType faultAfter = sim.Snapshot.WarningFault != RelayAFaultType.None ? sim.Snapshot.WarningFault : sim.Snapshot.ActiveFault;
            if (faultAfter == RelayAFaultType.None)
            {
                throw new System.InvalidOperationException("EmergencyStop improperly erased the scheduled/active fault!");
            }
        }
        finally
        {
            Object.DestroyImmediate(config);
        }
    }

    private static void TestElectricalFaultSolvableAndNonLooping()
    {
        RelayAConfig config = ScriptableObject.CreateInstance<RelayAConfig>();
        try
        {
            RelayASimulation sim = new RelayASimulation();
            sim.Initialize(config);
            sim.SetControls(30f, 30f, 30f);
            sim.Start();

            for (int i = 0; i < 200 && sim.Snapshot.ActiveFault == RelayAFaultType.None; i++)
            {
                sim.Tick(0.1f);
            }

            if (sim.Snapshot.ActiveFault == RelayAFaultType.None)
                throw new System.InvalidOperationException("Active fault never triggered.");

            RelayAFaultType activeType = sim.Snapshot.ActiveFault;
            Vector3 faultOffset = config.GetFaultOffset(activeType);
            if (faultOffset == Vector3.zero)
                throw new System.InvalidOperationException("Active fault offset is zero.");

            for (int i = 0; i < Mathf.CeilToInt(config.FaultDurationSeconds * 12); i++)
            {
                sim.Tick(0.1f);
            }

            if (sim.Snapshot.ActiveFault != RelayAFaultType.None)
                throw new System.InvalidOperationException("Fault did not terminate after its duration.");

            for (int i = 0; i < 100; i++)
            {
                sim.Tick(0.1f);
                if (sim.Snapshot.WarningFault != RelayAFaultType.None || sim.Snapshot.ActiveFault != RelayAFaultType.None)
                    throw new System.InvalidOperationException("Fault repeated, violating the single fault per session rule.");
            }
        }
        finally
        {
            Object.DestroyImmediate(config);
        }
    }

    private static void TestDoesNotGoOnlineBeforeTwelveSeconds()
    {
        RelayAConfig config = ScriptableObject.CreateInstance<RelayAConfig>();
        try
        {
            RelayASimulation sim = new RelayASimulation();
            sim.Initialize(config);
            Vector3 solved = config.SolvedControls;
            sim.SetControls(solved.x, solved.y, solved.z);
            sim.Start();

            for (int i = 0; i < 40 && !sim.Snapshot.IsStable; i++) sim.Tick(0.1f);

            while (sim.Snapshot.StabilitySeconds < 11.8f && !sim.Snapshot.IsOnline)
            {
                sim.Tick(0.1f);
            }

            if (sim.Snapshot.IsOnline)
                throw new System.InvalidOperationException("Relay A went online prematurely before 12 seconds.");

            for (int i = 0; i < 10 && !sim.Snapshot.IsOnline; i++)
            {
                sim.Tick(0.1f);
            }

            if (!sim.Snapshot.IsOnline)
                throw new System.InvalidOperationException("Relay A failed to go online after 12.0 seconds.");
        }
        finally
        {
            Object.DestroyImmediate(config);
        }
    }

    private static void TestOnlineStateLocksControls()
    {
        RelayAConfig config = ScriptableObject.CreateInstance<RelayAConfig>();
        try
        {
            RelayASimulation sim = new RelayASimulation();
            sim.Initialize(config);
            sim.ForceCompleteForAuthoritativeSync();

            if (!sim.Snapshot.IsOnline)
                throw new System.InvalidOperationException("Simulation is not Online after ForceComplete.");

            Vector3 controlsBefore = sim.Snapshot.Controls;
            sim.SetControls(99f, 99f, 99f);

            if (sim.Snapshot.Controls != controlsBefore)
                throw new System.InvalidOperationException("Controls were modified while Online.");

            sim.Start();
            if (sim.Snapshot.IsRunning)
                throw new System.InvalidOperationException("Simulation was restarted while Online.");

            if (sim.Snapshot.Status != RelayAStatus.Online)
                throw new System.InvalidOperationException($"Status is not Online: {sim.Snapshot.Status}");
        }
        finally
        {
            Object.DestroyImmediate(config);
        }
    }

    private static void AssertInRange(float value, Vector2 range, string label)
    {
        if (value < range.x || value > range.y)
        {
            throw new System.InvalidOperationException($"{label} {value} outside {range.x}-{range.y}.");
        }
    }

    private static RelayAUIController BuildUi(Transform prefabRoot)
    {
        Transform existing = prefabRoot.Find("RelayA_UI");
        if (existing != null)
        {
            Object.DestroyImmediate(existing.gameObject);
        }

        Sprite panelSprite = LoadSprite("Assets/_Project/UI/BunkerSurvivalUI/Sprites/NineSlice/panel_industrial_main_normal_9slice.png");
        Sprite sidePanelSprite = LoadSprite("Assets/_Project/UI/BunkerSurvivalUI/Sprites/NineSlice/frame_security_terminal_normal_9slice.png");
        Sprite buttonSprite = LoadSprite("Assets/_Project/UI/BunkerSurvivalUI/Sprites/NineSlice/button_primary_normal_9slice.png");
        Sprite dangerButtonSprite = LoadSprite("Assets/_Project/UI/BunkerSurvivalUI/Sprites/NineSlice/button_danger_normal_9slice.png");
        Sprite warningIconSprite = LoadSprite("Assets/_Project/UI/BunkerSurvivalUI/Sprites/Icons/icon_hazard_sign.png");

        GameObject canvasObject = new GameObject("RelayA_UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup), typeof(RelayAUIController));
        canvasObject.transform.SetParent(prefabRoot, false);
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 80;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
        Stretch(canvasRect);

        GameObject panel = CreatePanel(canvasObject.transform, "PanelRoot", panelSprite, new Color(0.03f, 0.07f, 0.08f, 0.96f));
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(1160f, 720f);
        panelRect.anchoredPosition = Vector2.zero;

        TMP_Text facility = CreateText(panel.transform, "FacilityLabel", "ECHO FACILITY", 24f, new Vector2(40f, -24f), new Vector2(340f, 28f), TextAlignmentOptions.Left);
        TMP_Text relay = CreateText(panel.transform, "RelayLabel", "POWER RELAY A", 40f, new Vector2(40f, -54f), new Vector2(520f, 48f), TextAlignmentOptions.Left);
        TMP_Text mode = CreateText(panel.transform, "ModeLabel", "VOLTAGE STABILIZATION", 20f, new Vector2(40f, -104f), new Vector2(520f, 26f), TextAlignmentOptions.Left);
        TMP_Text status = CreateText(
            panel.transform,
            "StatusLabel",
            "OFFLINE",
            32f,
            new Vector2(-40f, -40f),
            new Vector2(260f, 48f),
            TextAlignmentOptions.Right,
            new Vector2(1f, 1f),
            new Vector2(1f, 1f));

        GameObject monitoring = CreatePanel(panel.transform, "SystemMonitoring", sidePanelSprite, new Color(0.015f, 0.05f, 0.06f, 0.98f));
        SetAnchored(monitoring, new Vector2(40f, -170f), new Vector2(515f, 310f), new Vector2(0f, 1f));
        CreateText(monitoring.transform, "Title", "SYSTEM MONITORING", 22f, new Vector2(22f, -20f), new Vector2(360f, 30f), TextAlignmentOptions.Left);
        GaugeRefs voltage = CreateGauge(monitoring.transform, "Voltage", "Voltage", "SAFE 220-230 V\nRANGE 200-250 V", 0.40f, 0.60f, new Vector2(26f, -70f));
        GaugeRefs frequency = CreateGauge(monitoring.transform, "Frequency", "Frequency", "SAFE 49-51 Hz\nRANGE 45-55 Hz", 0.40f, 0.60f, new Vector2(26f, -155f));
        GaugeRefs load = CreateGauge(monitoring.transform, "Load", "Load Balance", "SAFE 47-53%\nRANGE 35-65%", 0.40f, 0.60f, new Vector2(26f, -240f));

        GameObject controls = CreatePanel(panel.transform, "ManualControl", sidePanelSprite, new Color(0.015f, 0.05f, 0.06f, 0.98f));
        SetAnchored(controls, new Vector2(40f, -500f), new Vector2(515f, 170f), new Vector2(0f, 1f));
        CreateText(controls.transform, "Title", "MANUAL CONTROL", 22f, new Vector2(22f, -18f), new Vector2(360f, 30f), TextAlignmentOptions.Left);
        SliderRefs generator = CreateSlider(controls.transform, "Generator", "Generator Output", new Vector2(26f, -58f), 0.52f, 0.64f);
        SliderRefs frequencyControl = CreateSlider(controls.transform, "FrequencyRegulator", "Frequency Regulator", new Vector2(26f, -98f), 0.36f, 0.48f);
        SliderRefs loadControl = CreateSlider(controls.transform, "LoadDistribution", "Load Distribution", new Vector2(26f, -138f), 0.48f, 0.60f);

        GameObject stability = CreatePanel(panel.transform, "StabilityVerification", sidePanelSprite, new Color(0.015f, 0.05f, 0.06f, 0.98f));
        SetAnchored(stability, new Vector2(595f, -170f), new Vector2(525f, 150f), new Vector2(0f, 1f));
        TMP_Text stabilityLabel = CreateText(stability.transform, "StabilityLabel", "STABILITY VERIFICATION\nProgress: 0 / 12 seconds", 22f, new Vector2(24f, -24f), new Vector2(430f, 62f), TextAlignmentOptions.Left);
        Image stabilityFill = CreateBar(stability.transform, "StabilityProgress", new Vector2(26f, -100f), new Vector2(472f, 24f), new Color(0.3f, 1f, 0.55f, 1f));
        TMP_Text instabilityReason = CreateText(stability.transform, "InstabilityReason", "System offline. Start stabilization.", 18f, new Vector2(26f, -124f), new Vector2(472f, 28f), TextAlignmentOptions.Left);

        GameObject warning = CreatePanel(panel.transform, "WarningPanel", sidePanelSprite, new Color(0.08f, 0.025f, 0.02f, 0.98f));
        SetAnchored(warning, new Vector2(595f, -340f), new Vector2(525f, 190f), new Vector2(0f, 1f));
        Image warningIcon = CreateImage(warning.transform, "WarningIcon", warningIconSprite, new Color(1f, 0.78f, 0.18f, 1f));
        SetAnchored(warningIcon.gameObject, new Vector2(24f, -24f), new Vector2(52f, 52f), new Vector2(0f, 1f));
        TMP_Text warningTitle = CreateText(warning.transform, "WarningTitle", "NO ACTIVE FAULT", 24f, new Vector2(88f, -20f), new Vector2(390f, 32f), TextAlignmentOptions.Left);
        TMP_Text warningDescription = CreateText(warning.transform, "WarningDescription", "Monitoring relay load and grid phase.", 18f, new Vector2(88f, -58f), new Vector2(395f, 48f), TextAlignmentOptions.Left);
        TMP_Text warningTimer = CreateText(warning.transform, "WarningTimer", "STANDBY", 24f, new Vector2(26f, -122f), new Vector2(230f, 34f), TextAlignmentOptions.Left);
        TMP_Text overload = CreateText(warning.transform, "OverloadLabel", "OVERLOAD: CLEAR", 24f, new Vector2(272f, -122f), new Vector2(225f, 34f), TextAlignmentOptions.Right);

        Button start = CreateButton(panel.transform, "StartButton", "START STABILIZATION", buttonSprite, new Vector2(595f, -570f), new Vector2(250f, 58f));
        Button stop = CreateButton(panel.transform, "EmergencyStopButton", "EMERGENCY STOP", dangerButtonSprite, new Vector2(870f, -570f), new Vector2(250f, 58f));
        Button close = CreateButton(panel.transform, "CloseButton", "CLOSE", buttonSprite, new Vector2(870f, -640f), new Vector2(250f, 50f));

        RelayAUIController ui = canvasObject.GetComponent<RelayAUIController>();
        SerializedObject so = new SerializedObject(ui);
        SetObject(so, "panelRoot", panel);
        SetObject(so, "canvasGroup", canvasObject.GetComponent<CanvasGroup>());
        SetObject(so, "facilityLabel", facility);
        SetObject(so, "relayLabel", relay);
        SetObject(so, "modeLabel", mode);
        SetObject(so, "statusLabel", status);
        SetObject(so, "voltageValue", voltage.Value);
        SetObject(so, "voltageStatus", voltage.Status);
        SetObject(so, "voltageTrend", voltage.Trend);
        SetObject(so, "voltageGauge", voltage.Fill);
        SetObject(so, "frequencyValue", frequency.Value);
        SetObject(so, "frequencyStatus", frequency.Status);
        SetObject(so, "frequencyTrend", frequency.Trend);
        SetObject(so, "frequencyGauge", frequency.Fill);
        SetObject(so, "loadValue", load.Value);
        SetObject(so, "loadStatus", load.Status);
        SetObject(so, "loadTrend", load.Trend);
        SetObject(so, "loadGauge", load.Fill);
        SetObject(so, "generatorSlider", generator.Slider);
        SetObject(so, "generatorValue", generator.Value);
        SetObject(so, "frequencySlider", frequencyControl.Slider);
        SetObject(so, "frequencyControlValue", frequencyControl.Value);
        SetObject(so, "loadSlider", loadControl.Slider);
        SetObject(so, "loadControlValue", loadControl.Value);
        SetObject(so, "stabilityLabel", stabilityLabel);
        SetObject(so, "stabilityFill", stabilityFill);
        SetObject(so, "instabilityReasonLabel", instabilityReason);
        SetObject(so, "warningIcon", warningIcon);
        SetObject(so, "warningTitle", warningTitle);
        SetObject(so, "warningDescription", warningDescription);
        SetObject(so, "warningTimer", warningTimer);
        SetObject(so, "overloadLabel", overload);
        SetObject(so, "startButton", start);
        SetObject(so, "emergencyStopButton", stop);
        SetObject(so, "closeButton", close);
        so.ApplyModifiedPropertiesWithoutUndo();

        panel.SetActive(false);
        return ui;
    }

    private static GaugeRefs CreateGauge(Transform parent, string name, string label, string safeBand, float safeMin01, float safeMax01, Vector2 position)
    {
        GameObject root = new GameObject(name, typeof(RectTransform));
        root.transform.SetParent(parent, false);
        SetAnchored(root, position, new Vector2(462f, 70f), new Vector2(0f, 1f));
        CreateText(root.transform, "Label", $"{label}\n{safeBand}", 16f, new Vector2(0f, 0f), new Vector2(170f, 54f), TextAlignmentOptions.Left);
        TMP_Text value = CreateText(root.transform, "Value", "--", 22f, new Vector2(175f, 0f), new Vector2(100f, 30f), TextAlignmentOptions.Right);
        TMP_Text status = CreateText(root.transform, "Status", "OPTIMAL", 15f, new Vector2(280f, 2f), new Vector2(85f, 26f), TextAlignmentOptions.Center);
        TMP_Text trend = CreateText(root.transform, "Trend", "STABLE →", 15f, new Vector2(370f, 2f), new Vector2(90f, 26f), TextAlignmentOptions.Right);
        Image fill = CreateBar(root.transform, "Gauge", new Vector2(175f, -38f), new Vector2(285f, 18f), new Color(0.35f, 1f, 0.58f, 1f), safeMin01, safeMax01);
        return new GaugeRefs(value, status, trend, fill);
    }

    private static SliderRefs CreateSlider(Transform parent, string name, string label, Vector2 position, float safeMin01 = -1f, float safeMax01 = -1f)
    {
        GameObject root = new GameObject(name, typeof(RectTransform));
        root.transform.SetParent(parent, false);
        SetAnchored(root, position, new Vector2(462f, 34f), new Vector2(0f, 1f));
        CreateText(root.transform, "Label", label, 17f, new Vector2(0f, -1f), new Vector2(180f, 28f), TextAlignmentOptions.Left);
        Slider slider = CreateSliderControl(root.transform, "Slider", new Vector2(190f, -2f), new Vector2(200f, 22f), safeMin01, safeMax01);
        TMP_Text value = CreateText(root.transform, "Value", "0%", 17f, new Vector2(404f, -1f), new Vector2(58f, 28f), TextAlignmentOptions.Right);
        return new SliderRefs(slider, value);
    }

    private static Slider CreateSliderControl(Transform parent, string name, Vector2 position, Vector2 size, float safeMin01 = -1f, float safeMax01 = -1f)
    {
        Sprite whitePixel = LoadSprite("Assets/_Project/UI/white_pixel.png");
        GameObject root = new GameObject(name, typeof(RectTransform), typeof(Slider));
        root.transform.SetParent(parent, false);
        SetAnchored(root, position, size, new Vector2(0f, 1f));

        Image background = CreateImage(root.transform, "Background", whitePixel, new Color(0.03f, 0.13f, 0.14f, 1f));
        Stretch(background.rectTransform);

        if (safeMin01 >= 0f && safeMax01 >= safeMin01)
        {
            GameObject safeZone = CreatePanel(root.transform, "SliderSafeZone", whitePixel, new Color(0.12f, 0.60f, 0.35f, 0.40f));
            RectTransform safeRt = safeZone.GetComponent<RectTransform>();
            safeRt.anchorMin = new Vector2(safeMin01, 0f);
            safeRt.anchorMax = new Vector2(safeMax01, 1f);
            safeRt.offsetMin = Vector2.zero;
            safeRt.offsetMax = Vector2.zero;

            GameObject centerNotch = CreatePanel(safeZone.transform, "TargetNotch", whitePixel, new Color(0.35f, 1f, 0.58f, 0.65f));
            RectTransform notchRt = centerNotch.GetComponent<RectTransform>();
            notchRt.anchorMin = new Vector2(0.5f, 0f);
            notchRt.anchorMax = new Vector2(0.5f, 1f);
            notchRt.pivot = new Vector2(0.5f, 0.5f);
            notchRt.sizeDelta = new Vector2(2f, 0f);
            notchRt.anchoredPosition = Vector2.zero;
        }

        Image fill = CreateImage(root.transform, "Fill", whitePixel, new Color(0.35f, 1f, 0.58f, 1f));
        fill.raycastTarget = false;
        RectTransform fillRect = fill.rectTransform;
        fillRect.anchorMin = new Vector2(0f, 0f);
        fillRect.anchorMax = new Vector2(0f, 1f);
        fillRect.pivot = new Vector2(0f, 0.5f);
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;

        Image handle = CreateImage(root.transform, "Handle", whitePixel, new Color(0.9f, 1f, 0.92f, 1f));
        handle.raycastTarget = true;
        RectTransform handleRect = handle.rectTransform;
        handleRect.anchorMin = new Vector2(0f, 0f);
        handleRect.anchorMax = new Vector2(0f, 1f);
        handleRect.pivot = new Vector2(0.5f, 0.5f);
        handleRect.anchoredPosition = Vector2.zero;
        handleRect.sizeDelta = new Vector2(10f, 0f);

        Slider slider = root.GetComponent<Slider>();
        slider.fillRect = fillRect;
        slider.handleRect = handle.rectTransform;
        slider.targetGraphic = handle;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0f;
        slider.maxValue = 100f;
        return slider;
    }

    private static Button CreateButton(Transform parent, string name, string text, Sprite sprite, Vector2 position, Vector2 size)
    {
        GameObject root = CreatePanel(parent, name, sprite, new Color(0.1f, 0.22f, 0.24f, 0.98f));
        SetAnchored(root, position, size, new Vector2(0f, 1f));
        Button button = root.AddComponent<Button>();
        button.targetGraphic = root.GetComponent<Image>();
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.75f, 1f, 0.9f, 1f);
        colors.pressedColor = new Color(0.5f, 0.8f, 0.7f, 1f);
        colors.selectedColor = Color.white;
        colors.disabledColor = new Color(0.4f, 0.4f, 0.4f, 0.5f);
        button.colors = colors;
        CreateText(root.transform, "Label", text, 18f, Vector2.zero, size, TextAlignmentOptions.Center);
        return button;
    }

    private static Image CreateBar(Transform parent, string name, Vector2 position, Vector2 size, Color fillColor, float safeMin01 = -1f, float safeMax01 = -1f)
    {
        Sprite whitePixel = LoadSprite("Assets/_Project/UI/white_pixel.png");
        GameObject root = CreatePanel(parent, name + "Background", whitePixel, new Color(0.02f, 0.08f, 0.1f, 1f));
        SetAnchored(root, position, size, new Vector2(0f, 1f));

        if (safeMin01 >= 0f && safeMax01 >= safeMin01)
        {
            GameObject safeZone = CreatePanel(root.transform, name + "SafeZone", whitePixel, new Color(0.12f, 0.45f, 0.25f, 0.45f));
            RectTransform safeRt = safeZone.GetComponent<RectTransform>();
            safeRt.anchorMin = new Vector2(safeMin01, 0f);
            safeRt.anchorMax = new Vector2(safeMax01, 1f);
            safeRt.offsetMin = Vector2.zero;
            safeRt.offsetMax = Vector2.zero;
        }

        Image fill = CreateImage(root.transform, name + "Fill", whitePixel, fillColor);
        Stretch(fill.rectTransform);
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = 0;
        fill.fillAmount = 0f;
        return fill;
    }

    private static GameObject CreatePanel(Transform parent, string name, Sprite sprite, Color color)
    {
        GameObject root = new GameObject(name, typeof(RectTransform), typeof(Image));
        root.transform.SetParent(parent, false);
        Image image = root.GetComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.type = sprite != null ? Image.Type.Sliced : Image.Type.Simple;
        return root;
    }

    private static Image CreateImage(Transform parent, string name, Sprite sprite, Color color)
    {
        GameObject root = new GameObject(name, typeof(RectTransform), typeof(Image));
        root.transform.SetParent(parent, false);
        Image image = root.GetComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.type = sprite != null ? Image.Type.Sliced : Image.Type.Simple;
        return image;
    }

    private static TMP_Text CreateText(
        Transform parent,
        string name,
        string text,
        float size,
        Vector2 position,
        Vector2 rectSize,
        TextAlignmentOptions alignment,
        Vector2? anchor = null,
        Vector2? pivot = null)
    {
        GameObject root = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        root.transform.SetParent(parent, false);
        RectTransform rt = root.GetComponent<RectTransform>();
        Vector2 anc = anchor ?? new Vector2(0f, 1f);
        Vector2 piv = pivot ?? new Vector2(0f, 1f);
        rt.anchorMin = anc;
        rt.anchorMax = anc;
        rt.pivot = piv;
        rt.anchoredPosition = position;
        rt.sizeDelta = rectSize;

        TMP_Text label = root.GetComponent<TMP_Text>();
        label.text = text;
        label.fontSize = size;
        label.enableAutoSizing = false;
        label.alignment = alignment;
        label.color = new Color(0.84f, 1f, 0.88f, 1f);
        label.raycastTarget = false;
        return label;
    }

    private static void SetAnchored(GameObject target, Vector2 position, Vector2 size, Vector2 topLeftAnchor)
    {
        SetAnchored(target.GetComponent<RectTransform>(), position, size, topLeftAnchor);
    }

    private static void SetAnchored(RectTransform rect, Vector2 position, Vector2 size, Vector2 topLeftAnchor)
    {
        rect.anchorMin = topLeftAnchor;
        rect.anchorMax = topLeftAnchor;
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static T EnsureComponent<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();
        return component != null ? component : target.AddComponent<T>();
    }

    private static Sprite LoadSprite(string path)
    {
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static void EnsureFolder(string path)
    {
        string[] parts = path.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }

            current = next;
        }
    }

    private static void SetObject(SerializedObject serializedObject, string propertyName, Object value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
        {
            property.objectReferenceValue = value;
        }
    }

    private readonly struct GaugeRefs
    {
        public GaugeRefs(TMP_Text value, TMP_Text status, TMP_Text trend, Image fill)
        {
            Value = value;
            Status = status;
            Trend = trend;
            Fill = fill;
        }

        public TMP_Text Value { get; }
        public TMP_Text Status { get; }
        public TMP_Text Trend { get; }
        public Image Fill { get; }
    }

    private readonly struct SliderRefs
    {
        public SliderRefs(Slider slider, TMP_Text value)
        {
            Slider = slider;
            Value = value;
        }

        public Slider Slider { get; }
        public TMP_Text Value { get; }
    }

}
