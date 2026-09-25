using System.IO;
using System.Text;
using EchoProtocol.Networking;
using EchoProtocol.RelayA;
using EchoProtocol.RelayB;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// [InitializeOnLoad] - Disabled auto-run on domain reload to avoid overriding configured prefabs
public static class RelayBSetupBuilder
{
    private const string RelayBPrefabPath = "Assets/Prefabs/Gameplay/Imported/RelayB.prefab";
    private const string ConfigPath = "Assets/ScriptableObjects/RelayB/RelayB_Hard_Config.asset";
    private static bool _hasRun;

    /*
    static RelayBSetupBuilder()
    {
        EditorApplication.delayCall += AutoRunOnce;
    }
    */

    private static void AutoRunOnce()
    {
        if (_hasRun)
        {
            return;
        }

        _hasRun = true;
        Debug.Log("[RelayBSetupBuilder] InitializeOnLoad triggered: Running SetupRelayB and Tests...");
        try
        {
            SetupRelayB();
            RunRelayBEditModeTests();
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[RelayBSetupBuilder] Error during AutoRun: {ex}");
        }
    }

    [MenuItem("Tools/ECHO Protocol/Setup Relay B Signal Synchronization")]
    public static void SetupRelayB()
    {
        EnsureFolder("Assets/ScriptableObjects/RelayB");
        RelayBConfig config = AssetDatabase.LoadAssetAtPath<RelayBConfig>(ConfigPath);
        if (config == null)
        {
            config = ScriptableObject.CreateInstance<RelayBConfig>();
            config.InitializeDefaultPresetsIfEmpty();
            AssetDatabase.CreateAsset(config, ConfigPath);
        }
        else
        {
            config.InitializeDefaultPresetsIfEmpty();
            EditorUtility.SetDirty(config);
        }

        if (!File.Exists(RelayBPrefabPath))
        {
            Debug.LogError($"[RelayBSetupBuilder] RelayB prefab not found at: {RelayBPrefabPath}");
            return;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(RelayBPrefabPath);
        try
        {
            RelayBController controller = EnsureComponent<RelayBController>(root);
            RelayBInteraction interaction = EnsureComponent<RelayBInteraction>(root);
            RelayBNetworkState networkState = EnsureComponent<RelayBNetworkState>(root);

            AudioSource audio = EnsureComponent<AudioSource>(root);
            audio.playOnAwake = false;
            audio.spatialBlend = 1f;
            audio.rolloffMode = AudioRolloffMode.Linear;
            audio.maxDistance = 16f;

            RelayBUIController ui = BuildUi(root.transform);

            SerializedObject controllerSo = new SerializedObject(controller);
            SetObject(controllerSo, "config", config);
            SetObject(controllerSo, "ui", ui);
            SetObject(controllerSo, "audioSource", audio);
            controllerSo.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject interactionSo = new SerializedObject(interaction);
            SetObject(interactionSo, "controller", controller);
            interactionSo.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject networkSo = new SerializedObject(networkState);
            SetObject(networkSo, "controller", controller);
            networkSo.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, RelayBPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[RelayBSetupBuilder] Relay B Signal Synchronization UI, Controller, and references are ready.");
    }

    [MenuItem("Tools/ECHO Protocol/Run Relay B EditMode Tests")]
    public static void RunRelayBEditModeTests()
    {
        int passed = 0;
        int failed = 0;
        StringBuilder failures = new StringBuilder();

        RunTest("Presets_AllFourHaveSolvableSolutions", TestAllPresetsSolvable, ref passed, ref failed, failures);
        RunTest("PhaseMath_CyclicDistanceCalculatesCorrectly", TestCyclicPhaseMath, ref passed, ref failed, failures);
        RunTest("FrequencyMath_ToleranceEvaluationAccurate", TestFrequencyTolerance, ref passed, ref failed, failures);
        RunTest("WrongChannels_CannotAchieveSyncOrMatchThreshold", TestWrongChannelsCannotSync, ref passed, ref failed, failures);
        RunTest("WaveformMismatch_Rejection", TestWaveformMismatchRejection, ref passed, ref failed, failures);
        RunTest("HoldTimer_ProgressesOnlyWhenSynchronized", TestHoldTimerOnlyWhenSynchronized, ref passed, ref failed, failures);
        RunTest("ProgressBar_FillAmountStrictlyMatchesRatio", TestProgressBarFillAmountMatchesRatio, ref passed, ref failed, failures);
        RunTest("InstabilityGrace_RecoversWithinHalfSecond", TestInstabilityGraceRecovery, ref passed, ref failed, failures);
        RunTest("InstabilityGrace_ExceedingHalfSecondResetsProgress", TestInstabilityGraceExceededResets, ref passed, ref failed, failures);
        RunTest("SignalDrift_WarningTriggersThreeSecondsPrior", TestDriftWarningThreeSecondsPrior, ref passed, ref failed, failures);
        RunTest("SignalDrift_ActivationShiftsEffectiveTarget", TestDriftActivationShiftsEffectiveTarget, ref passed, ref failed, failures);
        RunTest("SignalDrift_OccursOnlyOncePerSession", TestDriftOccursOnlyOncePerSession, ref passed, ref failed, failures);
        RunTest("ChannelSwitch_CancelsOngoingSyncAndResetsTimer", TestChannelSwitchCancelsOngoingSyncAndResets, ref passed, ref failed, failures);
        RunTest("FullRun_ReachesEightSecondsAndLocksOnline", TestFullCompletionLocksOnline, ref passed, ref failed, failures);
        RunTest("OnlineState_LocksInteractiveControls", TestOnlineStateLocksInteractiveControls, ref passed, ref failed, failures);

        string status = failed == 0 && passed > 0 ? "PASS" : "FAIL";
        string summary = $"status={status} passed={passed} failed={failed} skipped=0";
        string logFolder = Path.Combine(Application.dataPath, "../Logs");
        if (!Directory.Exists(logFolder))
        {
            Directory.CreateDirectory(logFolder);
        }

        string outputPath = Path.Combine(logFolder, "RelayBEditModeTestResult.txt");
        File.WriteAllText(outputPath, summary + "\n" + failures);

        if (failed == 0)
        {
            Debug.Log("[RelayB-TESTS] Finished: " + summary);
        }
        else
        {
            Debug.LogError("[RelayB-TESTS] Finished: " + summary + "\n" + failures);
        }

        try
        {
            DiagnoseRelayInteraction();
        }
        catch (System.Exception ex)
        {
            Debug.LogError("[RelayB-DIAGNOSE] Error running diagnosis: " + ex);
        }
    }

    public static void DiagnoseRelayInteraction()
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== RELAY INTERACTION DIAGNOSTIC REPORT ===");
        sb.AppendLine($"Timestamp: {System.DateTime.Now}");

        string currentScene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().path;
        sb.AppendLine($"Current Scene: {currentScene}");
        if (currentScene != "Assets/Scenes/SciFi_Warehouse.unity")
        {
            sb.AppendLine("Opening SciFi_Warehouse.unity...");
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/SciFi_Warehouse.unity", UnityEditor.SceneManagement.OpenSceneMode.Single);
        }

        // 1. Relay A
        sb.AppendLine("\n--- RELAY A ---");
        GameObject relayA = GameObject.Find("RelayA");
        if (relayA == null)
        {
            sb.AppendLine("ERROR: 'RelayA' NOT found in scene!");
        }
        else
        {
            InspectRelay(relayA, typeof(RelayAInteraction), typeof(RelayAController), sb);
        }

        // 2. Relay B
        sb.AppendLine("\n--- RELAY B ---");
        GameObject relayB = GameObject.Find("RelayB");
        if (relayB == null)
        {
            sb.AppendLine("ERROR: 'RelayB' NOT found in scene!");
        }
        else
        {
            InspectRelay(relayB, typeof(RelayBInteraction), typeof(RelayBController), sb);
        }

        // 3. Cameras & Players
        sb.AppendLine("\n--- CAMERAS ---");
        Camera[] cameras = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include);
        sb.AppendLine($"Total cameras: {cameras.Length}");
        foreach (var c in cameras)
        {
            sb.AppendLine($"  Camera '{c.name}' on '{c.gameObject.name}' (tag='{c.tag}', enabled={c.enabled}, active={c.gameObject.activeInHierarchy}, pos={c.transform.position})");
        }
        Camera mainCam = Camera.main;
        sb.AppendLine($"Camera.main is: {(mainCam != null ? mainCam.name + " on " + mainCam.gameObject.name : "NULL")}");

        sb.AppendLine("\n--- PLAYERS ---");
        var playerInteractions = Object.FindObjectsByType<PlayerInteraction>(FindObjectsInactive.Include);
        sb.AppendLine($"Total PlayerInteraction: {playerInteractions.Length}");
        foreach (var pi in playerInteractions)
        {
            var so = new SerializedObject(pi);
            var camProp = so.FindProperty("raycastCamera");
            var distProp = so.FindProperty("interactDistance");
            var layersProp = so.FindProperty("interactableLayers");
            var trigProp = so.FindProperty("triggerInteraction");

            sb.AppendLine($"  PlayerInteraction on '{pi.gameObject.name}' (enabled={pi.enabled}, active={pi.gameObject.activeInHierarchy}, pos={pi.transform.position})");
            sb.AppendLine($"    raycastCamera: {(camProp.objectReferenceValue != null ? camProp.objectReferenceValue.name : "null (defaults to Camera.main)")}");
            sb.AppendLine($"    interactDistance: {distProp.floatValue}");
            sb.AppendLine($"    interactableLayers: {layersProp.intValue}");
            sb.AppendLine($"    triggerInteraction: {(QueryTriggerInteraction)trigProp.enumValueIndex}");

            if (relayA != null) TestRay(pi.gameObject, relayA, distProp.floatValue, layersProp.intValue, (QueryTriggerInteraction)trigProp.enumValueIndex, sb);
            if (relayB != null) TestRay(pi.gameObject, relayB, distProp.floatValue, layersProp.intValue, (QueryTriggerInteraction)trigProp.enumValueIndex, sb);
        }

        var networkInteractors = Object.FindObjectsByType<NetworkPlayerInteractor>(FindObjectsInactive.Include);
        sb.AppendLine($"Total NetworkPlayerInteractor: {networkInteractors.Length}");
        foreach (var ni in networkInteractors)
        {
            sb.AppendLine($"  NetworkPlayerInteractor on '{ni.gameObject.name}' (enabled={ni.enabled}, active={ni.gameObject.activeInHierarchy}, pos={ni.transform.position})");
        }

        sb.AppendLine("\n--- SIMULATED CLOSE-RANGE INTERACTION TEST ---");
        if (relayA != null && playerInteractions.Length > 0)
        {
            var p = playerInteractions[0].gameObject;
            var interA = relayA.GetComponent<RelayAInteraction>();
            sb.AppendLine($"RelayA.CanInteract(player={p.name}) = {interA.CanInteract(p)}");

            // Place virtual ray 1.5m in front of RelayA pointing at RelayA collider center
            var colA = relayA.GetComponent<Collider>();
            Vector3 centerA = colA != null ? colA.bounds.center : (relayA.transform.position + Vector3.up * 1.2f);
            Vector3 frontA = centerA - Vector3.right * 1.5f;
            Vector3 dirA = (centerA - frontA).normalized;
            if (Physics.Raycast(frontA, dirA, out RaycastHit hitA, 3.0f))
            {
                sb.AppendLine($"Ray from 1.5m in front of RelayA hit '{hitA.collider.gameObject.name}' (layer={LayerMask.LayerToName(hitA.collider.gameObject.layer)})");
                var comp = hitA.collider.GetComponentInParent<IInteractable>();
                sb.AppendLine($"  GetComponentInParent<IInteractable>: {(comp != null ? comp.GetType().Name : "NULL")}");
            }
            else
            {
                sb.AppendLine($"Ray from 1.5m in front of RelayA MISSED RelayA collider! BoxCollider may be offset or wrong size!");
            }

            // Test OpenUI invocation directly
            try
            {
                interA.Interact(p);
                sb.AppendLine("RelayA.Interact(player) called successfully without throwing exception.");
                var ctrlA = relayA.GetComponent<RelayAController>();
                ctrlA.CloseUI();
            }
            catch (System.Exception ex)
            {
                sb.AppendLine($"ERROR calling RelayA.Interact: {ex}");
            }
        }

        if (relayB != null && playerInteractions.Length > 0)
        {
            var p = playerInteractions[0].gameObject;
            var interB = relayB.GetComponent<RelayBInteraction>();
            sb.AppendLine($"RelayB.CanInteract(player={p.name}) = {interB.CanInteract(p)}");

            // Place virtual ray 1.5m in front of RelayB pointing at RelayB center
            var colB = relayB.GetComponent<Collider>();
            Vector3 centerB = colB != null ? colB.bounds.center : (relayB.transform.position + Vector3.up * 1.0f);
            Vector3 frontB = centerB + Vector3.forward * 1.5f;
            Vector3 dirB = (centerB - frontB).normalized;
            if (Physics.Raycast(frontB, dirB, out RaycastHit hitB, 3.0f))
            {
                sb.AppendLine($"Ray from 1.5m in front of RelayB hit '{hitB.collider.gameObject.name}' (layer={LayerMask.LayerToName(hitB.collider.gameObject.layer)})");
                var comp = hitB.collider.GetComponentInParent<IInteractable>();
                sb.AppendLine($"  GetComponentInParent<IInteractable>: {(comp != null ? comp.GetType().Name : "NULL")}");
            }
            else
            {
                sb.AppendLine($"Ray from 1.5m in front of RelayB MISSED RelayB collider! BoxCollider may be offset or wrong size!");
            }

            // Test OpenUI invocation directly
            try
            {
                interB.Interact(p);
                sb.AppendLine("RelayB.Interact(player) called successfully without throwing exception.");
                var ctrlB = relayB.GetComponent<RelayBController>();
                ctrlB.CloseUI();
            }
            catch (System.Exception ex)
            {
                sb.AppendLine($"ERROR calling RelayB.Interact: {ex}");
            }
        }

        sb.AppendLine("\n--- EVENT SYSTEM ---");
        var eventSystems = Object.FindObjectsByType<UnityEngine.EventSystems.EventSystem>(FindObjectsInactive.Include);
        sb.AppendLine($"Total EventSystems in scene: {eventSystems.Length}");
        foreach (var es in eventSystems)
        {
            sb.AppendLine($"  EventSystem on '{es.gameObject.name}', enabled={es.enabled}, active={es.gameObject.activeInHierarchy}");
            var modules = es.GetComponents<UnityEngine.EventSystems.BaseInputModule>();
            foreach (var m in modules)
            {
                sb.AppendLine($"    Module: {m.GetType().FullName}, enabled={m.enabled}");
            }
        }

        GameObject fp = GameObject.Find("FirstPersonPlayer");
        if (fp != null)
        {
            sb.AppendLine($"\nFirstPersonPlayer at {fp.transform.position}, active={fp.activeInHierarchy}");
            if (fp.activeSelf)
            {
                fp.SetActive(false);
                UnityEditor.EditorUtility.SetDirty(fp);
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
                UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
                sb.AppendLine("Deactivated obsolete FirstPersonPlayer to prevent duplicate Camera/AudioListener/Input conflicts.");
            }
            foreach (var c in fp.GetComponents<Component>())
            {
                sb.AppendLine($"    Component: {c.GetType().FullName}");
            }
        }

        string outPath = Path.Combine(Application.dataPath, "../Logs/RelayInteractionDiagnostics.txt");
        File.WriteAllText(outPath, sb.ToString());
        Debug.Log("[RelayB-DIAGNOSE] Written to: " + outPath);
    }

    private static void InspectRelay(GameObject obj, System.Type interactionType, System.Type controllerType, StringBuilder sb)
    {
        sb.AppendLine($"  GameObject: '{obj.name}', Layer: {obj.layer} ({LayerMask.LayerToName(obj.layer)}), Tag: '{obj.tag}', Active: {obj.activeInHierarchy}, Pos: {obj.transform.position}");
        var colliders = obj.GetComponentsInChildren<Collider>(true);
        sb.AppendLine($"  Total Colliders in hierarchy: {colliders.Length}");
        foreach (var c in colliders)
        {
            sb.AppendLine($"    Collider on '{c.gameObject.name}': {c.GetType().Name}, enabled={c.enabled}, isTrigger={c.isTrigger}, layer={c.gameObject.layer} ({LayerMask.LayerToName(c.gameObject.layer)}), bounds center={c.bounds.center} size={c.bounds.size}");
            if (c is BoxCollider bc)
            {
                sb.AppendLine($"      BoxCollider local center={bc.center}, local size={bc.size}");
            }
        }

        var renderers = obj.GetComponentsInChildren<Renderer>(true);
        sb.AppendLine($"  Total Renderers in hierarchy: {renderers.Length}");
        foreach (var r in renderers)
        {
            sb.AppendLine($"    Renderer on '{r.gameObject.name}': enabled={r.enabled}, localPos={r.transform.localPosition}, worldCenter={r.bounds.center}, worldSize={r.bounds.size}");
        }

        var inter = obj.GetComponent(interactionType) as IInteractable;
        if (inter != null)
        {
            sb.AppendLine($"  {interactionType.Name}: Prompt='{inter.InteractionPrompt}', CanInteract(null)={inter.CanInteract(null)}");
            var so = new SerializedObject(obj.GetComponent(interactionType));
            var ctrlProp = so.FindProperty("controller");
            sb.AppendLine($"    controller ref: {(ctrlProp.objectReferenceValue != null ? ctrlProp.objectReferenceValue.name : "NULL")}");
        }
        else
        {
            sb.AppendLine($"  ERROR: {interactionType.Name} NOT found on root!");
        }

        var ctrl = obj.GetComponent(controllerType);
        if (ctrl != null)
        {
            var so = new SerializedObject(ctrl);
            var uiProp = so.FindProperty("ui");
            var cfgProp = so.FindProperty("config");
            sb.AppendLine($"  {controllerType.Name}: ui ref={(uiProp.objectReferenceValue != null ? uiProp.objectReferenceValue.name : "NULL")}, config ref={(cfgProp.objectReferenceValue != null ? cfgProp.objectReferenceValue.name : "NULL")}");
        }
        else
        {
            sb.AppendLine($"  ERROR: {controllerType.Name} NOT found on root!");
        }
    }

    private static void TestRay(GameObject player, GameObject target, float maxDist, int mask, QueryTriggerInteraction trig, StringBuilder sb)
    {
        Vector3 playerPos = player.transform.position;
        Vector3 targetPos = target.transform.position;
        float dist = Vector3.Distance(playerPos, targetPos);
        sb.AppendLine($"    -> Test to '{target.name}': Euclidean distance = {dist:F2}m (Max interact = {maxDist:F2}m)");

        Vector3 rayOrigin = playerPos + Vector3.up * 1.65f;
        Vector3 rayTarget = targetPos + Vector3.up * 1.0f;
        Vector3 rayDir = (rayTarget - rayOrigin).normalized;
        float rayDist = Vector3.Distance(rayOrigin, rayTarget);

        if (Physics.Raycast(rayOrigin, rayDir, out RaycastHit hit, 20f, mask, trig))
        {
            sb.AppendLine($"       Ray hit '{hit.collider.gameObject.name}' at distance {hit.distance:F2}m (Layer={LayerMask.LayerToName(hit.collider.gameObject.layer)})");
            var hitInter = hit.collider.GetComponentInParent<IInteractable>();
            sb.AppendLine($"       Hit has IInteractable: {(hitInter != null ? hitInter.GetType().Name : "NONE")}");
        }
        else
        {
            sb.AppendLine($"       Ray hit NOTHING within 20m!");
        }
    }

    private static void RunTest(string name, System.Action test, ref int passed, ref int failed, StringBuilder failures)
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

    private static void TestAllPresetsSolvable()
    {
        RelayBConfig config = ScriptableObject.CreateInstance<RelayBConfig>();
        config.InitializeDefaultPresetsIfEmpty();

        try
        {
            if (config.Presets.Count < 4)
            {
                throw new System.InvalidOperationException($"Expected at least 4 presets, got {config.Presets.Count}");
            }

            for (int i = 0; i < config.Presets.Count; i++)
            {
                RelayBPreset preset = config.Presets[i];
                RelayBSignalSimulation sim = new RelayBSignalSimulation();
                sim.Initialize(config, i);

                // Select correct channel and set exact target freq and phase
                sim.SelectChannel(preset.CorrectChannelIndex);
                sim.SetFrequency(preset.TargetFrequency);
                sim.SetPhase(preset.TargetPhase);

                bool isSync = sim.CheckIsSynchronized(out float freqErr, out float phaseErr);
                if (!isSync)
                {
                    throw new System.InvalidOperationException($"Preset {i} ({preset.PresetName}) failed to synchronize with target values. FreqErr: {freqErr}%, PhaseErr: {phaseErr}°");
                }

                float match = sim.EvaluateSignalMatch(freqErr, phaseErr);
                if (match < 95f)
                {
                    throw new System.InvalidOperationException($"Preset {i} match score too low: {match}% (expected >= 95%)");
                }
            }
        }
        finally
        {
            Object.DestroyImmediate(config);
        }
    }

    private static void TestCyclicPhaseMath()
    {
        // 359° and 1° -> 2° difference
        float diff1 = RelayBSignalSimulation.CalculatePhaseErrorDegrees(359f, 1f);
        if (Mathf.Abs(diff1 - 2f) > 0.01f)
        {
            throw new System.InvalidOperationException($"Phase 359° and 1° gave {diff1}°, expected 2°");
        }

        // 10° and 350° -> 20° difference
        float diff2 = RelayBSignalSimulation.CalculatePhaseErrorDegrees(10f, 350f);
        if (Mathf.Abs(diff2 - 20f) > 0.01f)
        {
            throw new System.InvalidOperationException($"Phase 10° and 350° gave {diff2}°, expected 20°");
        }

        // 180° and 180° -> 0° difference
        float diff3 = RelayBSignalSimulation.CalculatePhaseErrorDegrees(180f, 180f);
        if (Mathf.Abs(diff3) > 0.01f)
        {
            throw new System.InvalidOperationException($"Phase 180° and 180° gave {diff3}°, expected 0°");
        }
    }

    private static void TestFrequencyTolerance()
    {
        float target = 50f;
        // 51.5 kHz = exactly +3%
        float err3Percent = RelayBSignalSimulation.CalculateFrequencyErrorPercent(51.5f, target);
        if (Mathf.Abs(err3Percent - 3f) > 0.01f)
        {
            throw new System.InvalidOperationException($"Expected 3% error, got {err3Percent}%");
        }

        // 52.0 kHz = +4% (exceeds 3% tolerance)
        float err4Percent = RelayBSignalSimulation.CalculateFrequencyErrorPercent(52f, target);
        if (err4Percent <= 3f)
        {
            throw new System.InvalidOperationException($"Expected > 3% error for 52 kHz vs 50 kHz, got {err4Percent}%");
        }
    }

    private static void TestWrongChannelsCannotSync()
    {
        RelayBConfig config = ScriptableObject.CreateInstance<RelayBConfig>();
        config.InitializeDefaultPresetsIfEmpty();

        try
        {
            for (int p = 0; p < config.Presets.Count; p++)
            {
                RelayBPreset preset = config.Presets[p];
                for (int c = 0; c < 4; c++)
                {
                    if (c == preset.CorrectChannelIndex)
                    {
                        continue;
                    }

                    RelayBSignalSimulation sim = new RelayBSignalSimulation();
                    sim.Initialize(config, p);

                    sim.SelectChannel(c);
                    sim.SetFrequency(preset.TargetFrequency);
                    sim.SetPhase(preset.TargetPhase);

                    bool isSync = sim.CheckIsSynchronized(out float freqErr, out float phaseErr);
                    if (isSync)
                    {
                        throw new System.InvalidOperationException($"Preset {p} allowed wrong channel {c} to achieve synchronized state!");
                    }

                    float match = sim.EvaluateSignalMatch(freqErr, phaseErr);
                    if (match > 65f)
                    {
                        throw new System.InvalidOperationException($"Preset {p} wrong channel {c} achieved unrealistic match: {match}% (expected <= 65%)");
                    }
                }
            }
        }
        finally
        {
            Object.DestroyImmediate(config);
        }
    }

    private static void TestWaveformMismatchRejection()
    {
        RelayBConfig config = ScriptableObject.CreateInstance<RelayBConfig>();
        config.InitializeDefaultPresetsIfEmpty();

        try
        {
            RelayBPreset preset = config.Presets[0];
            RelayBSignalSimulation sim = new RelayBSignalSimulation();
            sim.Initialize(config, 0);

            // Channel 0 has Triangle waveform, preset 0 expects Sine
            sim.SelectChannel(0);
            sim.SetFrequency(preset.TargetFrequency);
            sim.SetPhase(preset.TargetPhase);

            bool isSync = sim.CheckIsSynchronized(out float freqErr, out float phaseErr);
            if (isSync)
            {
                throw new System.InvalidOperationException("Waveform mismatch was accepted as synchronized!");
            }

            float match = sim.EvaluateSignalMatch(freqErr, phaseErr);
            if (match > 65f)
            {
                throw new System.InvalidOperationException($"Mismatched waveform achieved {match}% match, expected <= 65%");
            }
        }
        finally
        {
            Object.DestroyImmediate(config);
        }
    }

    private static void TestHoldTimerOnlyWhenSynchronized()
    {
        RelayBConfig config = ScriptableObject.CreateInstance<RelayBConfig>();
        config.InitializeDefaultPresetsIfEmpty();

        try
        {
            RelayBSignalSimulation sim = new RelayBSignalSimulation();
            sim.Initialize(config, 0);

            sim.SelectChannel(1);
            sim.SetFrequency(10f); // far off target (42 kHz)
            sim.StartSynchronization();

            sim.Tick(1f);
            if (sim.Snapshot.SyncProgressSeconds > 0f)
            {
                throw new System.InvalidOperationException("Hold timer increased while desynchronized!");
            }

            // Now tune to exact solution
            sim.SetFrequency(42f);
            sim.SetPhase(90f);
            sim.Tick(1f);

            if (sim.Snapshot.SyncProgressSeconds < 0.99f)
            {
                throw new System.InvalidOperationException($"Hold timer did not increase when synchronized: {sim.Snapshot.SyncProgressSeconds}s");
            }
        }
        finally
        {
            Object.DestroyImmediate(config);
        }
    }

    private static void TestProgressBarFillAmountMatchesRatio()
    {
        RelayBConfig config = ScriptableObject.CreateInstance<RelayBConfig>();
        config.InitializeDefaultPresetsIfEmpty();

        try
        {
            RelayBSignalSimulation sim = new RelayBSignalSimulation();
            sim.Initialize(config, 0);

            if (sim.Snapshot.Progress01 != 0f)
            {
                throw new System.InvalidOperationException($"Progress01 before start was {sim.Snapshot.Progress01}, expected 0.0");
            }

            sim.SelectChannel(1);
            sim.SetFrequency(42f);
            sim.SetPhase(90f);
            sim.StartSynchronization();

            if (sim.Snapshot.Progress01 != 0f)
            {
                throw new System.InvalidOperationException($"Progress01 at start was {sim.Snapshot.Progress01}, expected 0.0");
            }

            // 2s / 8s = 0.25
            for (int i = 0; i < 20; i++)
            {
                if (sim.Snapshot.IsDriftActive) sim.SetPhase(90f + config.DriftPhaseOffset);
                sim.Tick(0.1f);
            }
            if (Mathf.Abs(sim.Snapshot.Progress01 - 0.25f) > 0.02f)
            {
                throw new System.InvalidOperationException($"Progress01 at 2s was {sim.Snapshot.Progress01}, expected 0.25");
            }

            // 4s / 8s = 0.50
            for (int i = 0; i < 20; i++)
            {
                if (sim.Snapshot.IsDriftActive) sim.SetPhase(90f + config.DriftPhaseOffset);
                sim.Tick(0.1f);
            }
            if (Mathf.Abs(sim.Snapshot.Progress01 - 0.50f) > 0.02f)
            {
                throw new System.InvalidOperationException($"Progress01 at 4s was {sim.Snapshot.Progress01}, expected 0.50");
            }

            // 6s / 8s = 0.75
            for (int i = 0; i < 20; i++)
            {
                if (sim.Snapshot.IsDriftActive) sim.SetPhase(90f + config.DriftPhaseOffset);
                sim.Tick(0.1f);
            }
            if (Mathf.Abs(sim.Snapshot.Progress01 - 0.75f) > 0.02f)
            {
                throw new System.InvalidOperationException($"Progress01 at 6s was {sim.Snapshot.Progress01}, expected 0.75");
            }

            // Cancel synchronization -> resets to 0
            sim.CancelSynchronization();
            if (sim.Snapshot.Progress01 != 0f)
            {
                throw new System.InvalidOperationException($"Progress01 after cancel was {sim.Snapshot.Progress01}, expected 0.0");
            }
        }
        finally
        {
            Object.DestroyImmediate(config);
        }
    }

    private static void TestInstabilityGraceRecovery()
    {
        RelayBConfig config = ScriptableObject.CreateInstance<RelayBConfig>();
        config.InitializeDefaultPresetsIfEmpty();

        try
        {
            RelayBSignalSimulation sim = new RelayBSignalSimulation();
            sim.Initialize(config, 0);

            sim.SelectChannel(1);
            sim.SetFrequency(42f);
            sim.SetPhase(90f);
            sim.StartSynchronization();

            for (int i = 0; i < 20; i++) sim.Tick(0.1f);
            float progressBefore = sim.Snapshot.SyncProgressSeconds;

            // Deviate for 0.3s (within 0.5s grace)
            sim.SetFrequency(15f);
            sim.Tick(0.3f);

            if (sim.Snapshot.SyncProgressSeconds <= 0f)
            {
                throw new System.InvalidOperationException("Progress reset prematurely within 0.3s grace period!");
            }

            // Recover back to 42.0 kHz
            sim.SetFrequency(42f);
            sim.Tick(0.2f);

            if (sim.Snapshot.SyncProgressSeconds < progressBefore)
            {
                throw new System.InvalidOperationException("Progress lost after recovering within grace period!");
            }
        }
        finally
        {
            Object.DestroyImmediate(config);
        }
    }

    private static void TestInstabilityGraceExceededResets()
    {
        RelayBConfig config = ScriptableObject.CreateInstance<RelayBConfig>();
        config.InitializeDefaultPresetsIfEmpty();

        try
        {
            RelayBSignalSimulation sim = new RelayBSignalSimulation();
            sim.Initialize(config, 0);

            sim.SelectChannel(1);
            sim.SetFrequency(42f);
            sim.SetPhase(90f);
            sim.StartSynchronization();

            for (int i = 0; i < 20; i++) sim.Tick(0.1f);

            // Deviate for 0.6s (exceeds 0.5s grace)
            sim.SetFrequency(15f);
            sim.Tick(0.6f);

            if (sim.Snapshot.SyncProgressSeconds > 0f)
            {
                throw new System.InvalidOperationException($"Progress did not reset after exceeding grace period: {sim.Snapshot.SyncProgressSeconds}s");
            }

            if (sim.Snapshot.Progress01 != 0f)
            {
                throw new System.InvalidOperationException($"Progress01 did not reset to 0: {sim.Snapshot.Progress01}");
            }
        }
        finally
        {
            Object.DestroyImmediate(config);
        }
    }

    private static void TestDriftWarningThreeSecondsPrior()
    {
        RelayBConfig config = ScriptableObject.CreateInstance<RelayBConfig>();
        config.InitializeDefaultPresetsIfEmpty();

        try
        {
            RelayBSignalSimulation sim = new RelayBSignalSimulation();
            sim.Initialize(config, 0);

            bool warningFired = false;
            sim.DriftWarning += () => warningFired = true;

            sim.SelectChannel(1);
            sim.SetFrequency(42f);
            sim.SetPhase(90f);
            sim.StartSynchronization();

            // Run for 0.6s (warning triggers at 3.5s - 3.0s = 0.5s)
            for (int i = 0; i < 6; i++) sim.Tick(0.1f);

            if (!warningFired || !sim.Snapshot.IsDriftWarning)
            {
                throw new System.InvalidOperationException($"Drift warning did not fire at 0.5s hold (IsDriftWarning: {sim.Snapshot.IsDriftWarning})");
            }
        }
        finally
        {
            Object.DestroyImmediate(config);
        }
    }

    private static void TestDriftActivationShiftsEffectiveTarget()
    {
        RelayBConfig config = ScriptableObject.CreateInstance<RelayBConfig>();
        config.InitializeDefaultPresetsIfEmpty();

        try
        {
            RelayBSignalSimulation sim = new RelayBSignalSimulation();
            sim.Initialize(config, 0);

            bool driftFired = false;
            sim.DriftTriggered += () => driftFired = true;

            sim.SelectChannel(1);
            sim.SetFrequency(42f);
            sim.SetPhase(90f);
            sim.StartSynchronization();

            // Run past 3.5s
            for (int i = 0; i < 36; i++) sim.Tick(0.1f);

            if (!driftFired || !sim.Snapshot.IsDriftActive)
            {
                throw new System.InvalidOperationException("Drift did not trigger at 3.5s hold!");
            }

            float expectedPhase = 90f + config.DriftPhaseOffset;
            if (Mathf.Abs(sim.Snapshot.TargetPhase - expectedPhase) > 0.01f)
            {
                throw new System.InvalidOperationException($"Target phase did not shift to {expectedPhase}°, got {sim.Snapshot.TargetPhase}°");
            }
        }
        finally
        {
            Object.DestroyImmediate(config);
        }
    }

    private static void TestDriftOccursOnlyOncePerSession()
    {
        RelayBConfig config = ScriptableObject.CreateInstance<RelayBConfig>();
        config.InitializeDefaultPresetsIfEmpty();

        try
        {
            RelayBSignalSimulation sim = new RelayBSignalSimulation();
            sim.Initialize(config, 0);

            int driftCount = 0;
            sim.DriftTriggered += () => driftCount++;

            sim.SelectChannel(1);
            sim.SetFrequency(42f);
            sim.SetPhase(90f);
            sim.StartSynchronization();

            // Trigger drift at 3.5s
            for (int i = 0; i < 36; i++) sim.Tick(0.1f);
            if (driftCount != 1)
            {
                throw new System.InvalidOperationException($"Expected drift count 1, got {driftCount}");
            }

            // Cause a reset by staying desynced for 0.6s
            sim.Tick(0.6f);
            if (sim.Snapshot.SyncProgressSeconds > 0f)
            {
                throw new System.InvalidOperationException("Progress did not reset after desync.");
            }

            // Now compensate and re-synchronize
            sim.SetPhase(90f + config.DriftPhaseOffset);
            // Run past 3.5s again
            for (int i = 0; i < 40; i++) sim.Tick(0.1f);

            if (driftCount != 1)
            {
                throw new System.InvalidOperationException($"Drift triggered again! Count: {driftCount} (should trigger once per solve session)");
            }
        }
        finally
        {
            Object.DestroyImmediate(config);
        }
    }

    private static void TestChannelSwitchCancelsOngoingSyncAndResets()
    {
        RelayBConfig config = ScriptableObject.CreateInstance<RelayBConfig>();
        config.InitializeDefaultPresetsIfEmpty();

        try
        {
            RelayBSignalSimulation sim = new RelayBSignalSimulation();
            sim.Initialize(config, 0);

            sim.SelectChannel(1);
            sim.SetFrequency(42f);
            sim.SetPhase(90f);
            sim.StartSynchronization();

            for (int i = 0; i < 20; i++) sim.Tick(0.1f);
            if (sim.Snapshot.SyncProgressSeconds < 1.9f)
            {
                throw new System.InvalidOperationException("Progress failed to accumulate before channel switch.");
            }

            // Switch to Channel 0
            sim.SelectChannel(0);

            if (sim.Snapshot.Status == RelayBStatus.Synchronizing)
            {
                throw new System.InvalidOperationException("Status is still Synchronizing after channel switch!");
            }

            if (sim.Snapshot.SyncProgressSeconds > 0f)
            {
                throw new System.InvalidOperationException($"Sync timer was not reset after channel switch: {sim.Snapshot.SyncProgressSeconds}s");
            }
        }
        finally
        {
            Object.DestroyImmediate(config);
        }
    }

    private static void TestFullCompletionLocksOnline()
    {
        RelayBConfig config = ScriptableObject.CreateInstance<RelayBConfig>();
        config.InitializeDefaultPresetsIfEmpty();

        try
        {
            RelayBSignalSimulation sim = new RelayBSignalSimulation();
            sim.Initialize(config, 0);

            bool completedFired = false;
            sim.Completed += () => completedFired = true;

            sim.SelectChannel(1);
            sim.SetFrequency(42f);
            sim.SetPhase(90f);
            sim.StartSynchronization();

            for (int i = 0; i < 150 && !sim.Snapshot.IsOnline; i++)
            {
                if (sim.Snapshot.IsDriftActive)
                {
                    sim.SetPhase(90f + config.DriftPhaseOffset);
                }

                sim.Tick(0.1f);
            }

            if (!sim.Snapshot.IsOnline)
            {
                throw new System.InvalidOperationException("Relay B did not reach Online status after 8 seconds.");
            }

            if (!completedFired)
            {
                throw new System.InvalidOperationException("Completed event did not fire.");
            }

            if (Mathf.Abs(sim.Snapshot.SyncProgressSeconds - config.HoldRequiredSeconds) > 0.01f)
            {
                throw new System.InvalidOperationException($"Progress timer not clamped to 8.0s: {sim.Snapshot.SyncProgressSeconds}s");
            }
        }
        finally
        {
            Object.DestroyImmediate(config);
        }
    }

    private static void TestOnlineStateLocksInteractiveControls()
    {
        RelayBConfig config = ScriptableObject.CreateInstance<RelayBConfig>();
        config.InitializeDefaultPresetsIfEmpty();

        try
        {
            RelayBSignalSimulation sim = new RelayBSignalSimulation();
            sim.Initialize(config, 0);

            sim.ForceCompleteForAuthoritativeSync();
            if (!sim.Snapshot.IsOnline)
            {
                throw new System.InvalidOperationException("Simulation failed to enter Online state.");
            }

            int chBefore = sim.SelectedChannelIndex;
            float freqBefore = sim.CurrentFrequency;
            float phaseBefore = sim.CurrentPhase;

            sim.SelectChannel(3);
            sim.SetFrequency(12.5f);
            sim.SetPhase(333f);
            sim.StartSynchronization();

            if (sim.SelectedChannelIndex != chBefore)
            {
                throw new System.InvalidOperationException("SelectChannel modified state while Online!");
            }

            if (Mathf.Abs(sim.CurrentFrequency - freqBefore) > 0.01f)
            {
                throw new System.InvalidOperationException("SetFrequency modified state while Online!");
            }

            if (Mathf.Abs(sim.CurrentPhase - phaseBefore) > 0.01f)
            {
                throw new System.InvalidOperationException("SetPhase modified state while Online!");
            }
        }
        finally
        {
            Object.DestroyImmediate(config);
        }
    }

    private static RelayBUIController BuildUi(Transform prefabRoot)
    {
        Transform existing = prefabRoot.Find("RelayB_UI");
        if (existing != null)
        {
            Object.DestroyImmediate(existing.gameObject);
        }

        Sprite panelSprite = LoadSprite("Assets/_Project/UI/BunkerSurvivalUI/Sprites/NineSlice/panel_industrial_main_normal_9slice.png");
        Sprite sidePanelSprite = LoadSprite("Assets/_Project/UI/BunkerSurvivalUI/Sprites/NineSlice/frame_security_terminal_normal_9slice.png");
        Sprite buttonSprite = LoadSprite("Assets/_Project/UI/BunkerSurvivalUI/Sprites/NineSlice/button_primary_normal_9slice.png");
        Sprite dangerButtonSprite = LoadSprite("Assets/_Project/UI/BunkerSurvivalUI/Sprites/NineSlice/button_danger_normal_9slice.png");

        GameObject canvasObject = new GameObject(
            "RelayB_UI",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster),
            typeof(CanvasGroup),
            typeof(RelayBUIController));

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

        // Main Panel (1180 x 740)
        GameObject panel = CreatePanel(canvasObject.transform, "PanelRoot", panelSprite, new Color(0.02f, 0.05f, 0.07f, 0.98f));
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(1180f, 740f);
        panelRect.anchoredPosition = Vector2.zero;

        // Header
        TMP_Text facility = CreateText(panel.transform, "FacilityLabel", "ECHO FACILITY", 22f, new Vector2(40f, -22f), new Vector2(320f, 26f), TextAlignmentOptions.Left);
        TMP_Text relay = CreateText(panel.transform, "RelayLabel", "DATA RELAY B", 36f, new Vector2(40f, -48f), new Vector2(480f, 44f), TextAlignmentOptions.Left);
        TMP_Text mode = CreateText(panel.transform, "ModeLabel", "SIGNAL SYNCHRONIZATION", 18f, new Vector2(40f, -92f), new Vector2(480f, 24f), TextAlignmentOptions.Left);
        TMP_Text status = CreateText(
            panel.transform,
            "StatusLabel",
            "OFFLINE",
            30f,
            new Vector2(-40f, -36f),
            new Vector2(320f, 44f),
            TextAlignmentOptions.Right,
            new Vector2(1f, 1f),
            new Vector2(1f, 1f));

        // Warning / Drift Banner
        TMP_Text warningBanner = CreateText(panel.transform, "WarningBanner", string.Empty, 18f, new Vector2(0f, -118f), new Vector2(600f, 28f), TextAlignmentOptions.Center, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));

        // Left Side: Oscilloscope Section (550 x 500)
        GameObject scopePanel = CreatePanel(panel.transform, "OscilloscopeSection", sidePanelSprite, new Color(0.015f, 0.07f, 0.085f, 0.92f));
        SetAnchored(scopePanel, new Vector2(40f, -155f), new Vector2(550f, 500f), new Vector2(0f, 1f));
        CreateText(scopePanel.transform, "Title", "OSCILLOSCOPE DUAL WAVEFORM", 20f, new Vector2(22f, -16f), new Vector2(400f, 28f), TextAlignmentOptions.Left);

        // Top Waveform: Reference Signal
        GameObject refBox = CreatePanel(scopePanel.transform, "ReferenceBox", null, new Color(0.01f, 0.035f, 0.045f, 0.95f));
        SetAnchored(refBox, new Vector2(20f, -50f), new Vector2(510f, 200f), new Vector2(0f, 1f));

        GameObject refWaveObj = new GameObject("WaveformRenderer", typeof(RectTransform), typeof(RelayBWaveformRenderer));
        refWaveObj.transform.SetParent(refBox.transform, false);
        Stretch(refWaveObj.GetComponent<RectTransform>());
        RelayBWaveformRenderer refRenderer = refWaveObj.GetComponent<RelayBWaveformRenderer>();
        refRenderer.raycastTarget = false;

        TMP_Text refLabel = CreateText(refBox.transform, "RefLabel", "REFERENCE SIGNAL", 15f, new Vector2(10f, -8f), new Vector2(490f, 22f), TextAlignmentOptions.Left);
        refLabel.color = new Color(0.3f, 0.85f, 0.95f, 0.9f);

        // Bottom Waveform: Current Signal
        GameObject curBox = CreatePanel(scopePanel.transform, "CurrentBox", null, new Color(0.01f, 0.035f, 0.045f, 0.95f));
        SetAnchored(curBox, new Vector2(20f, -265f), new Vector2(510f, 200f), new Vector2(0f, 1f));

        GameObject curWaveObj = new GameObject("WaveformRenderer", typeof(RectTransform), typeof(RelayBWaveformRenderer));
        curWaveObj.transform.SetParent(curBox.transform, false);
        Stretch(curWaveObj.GetComponent<RectTransform>());
        RelayBWaveformRenderer curRenderer = curWaveObj.GetComponent<RelayBWaveformRenderer>();
        curRenderer.raycastTarget = false;

        TMP_Text curLabel = CreateText(curBox.transform, "CurLabel", "CURRENT SIGNAL", 15f, new Vector2(10f, -8f), new Vector2(490f, 22f), TextAlignmentOptions.Left);
        curLabel.color = new Color(0.4f, 0.95f, 0.6f, 0.9f);

        // Right Side: Control & Synchronization Deck (530 x 500)
        GameObject controlPanel = CreatePanel(panel.transform, "ControlDeck", sidePanelSprite, new Color(0.02f, 0.06f, 0.075f, 0.92f));
        SetAnchored(controlPanel, new Vector2(610f, -155f), new Vector2(530f, 500f), new Vector2(0f, 1f));
        CreateText(controlPanel.transform, "Title", "FREQUENCY & PHASE CALIBRATION", 20f, new Vector2(22f, -16f), new Vector2(400f, 28f), TextAlignmentOptions.Left);

        // 4 Channel Buttons
        Button[] channelBtns = new Button[4];
        Image[] channelHls = new Image[4];
        string[] channelNames = { "CHANNEL 01", "CHANNEL 02", "CHANNEL 03", "CHANNEL 04" };
        Vector2[] channelPositions =
        {
            new Vector2(20f, -55f),
            new Vector2(145f, -55f),
            new Vector2(270f, -55f),
            new Vector2(395f, -55f)
        };

        for (int c = 0; c < 4; c++)
        {
            Button btn = CreateButton(controlPanel.transform, $"ChannelButton_{c + 1}", channelNames[c], buttonSprite, channelPositions[c], new Vector2(115f, 44f), 14f);
            channelBtns[c] = btn;
            channelHls[c] = btn.GetComponent<Image>();
        }

        // Sliders
        SliderRefs freqSlider = CreateSlider(controlPanel.transform, "FrequencySlider", "Frequency", new Vector2(20f, -125f), 10f, 100f, "kHz");
        SliderRefs phaseSlider = CreateSlider(controlPanel.transform, "PhaseSlider", "Phase Angle", new Vector2(20f, -185f), 0f, 360f, "°");

        // Synchronization Monitor Panel
        GameObject monitorBox = CreatePanel(controlPanel.transform, "SyncMonitor", null, new Color(0.015f, 0.04f, 0.05f, 0.95f));
        SetAnchored(monitorBox, new Vector2(20f, -245f), new Vector2(490f, 180f), new Vector2(0f, 1f));

        TMP_Text signalMatch = CreateText(monitorBox.transform, "SignalMatch", "SIGNAL MATCH: 0.0%", 18f, new Vector2(16f, -10f), new Vector2(450f, 24f), TextAlignmentOptions.Left);
        TMP_Text freqErr = CreateText(monitorBox.transform, "FreqError", "FREQ ERROR: --", 15f, new Vector2(16f, -34f), new Vector2(220f, 20f), TextAlignmentOptions.Left);
        TMP_Text phaseErr = CreateText(monitorBox.transform, "PhaseError", "PHASE ERROR: --", 15f, new Vector2(240f, -34f), new Vector2(230f, 20f), TextAlignmentOptions.Left);

        TMP_Text progress = CreateText(monitorBox.transform, "ProgressLabel", "SYNCHRONIZATION PROGRESS\nProgress: 0.0 / 8 seconds", 15f, new Vector2(16f, -56f), new Vector2(450f, 34f), TextAlignmentOptions.Left);
        Image progressFill = CreateBar(monitorBox.transform, "Progress", new Vector2(16f, -94f), new Vector2(458f, 18f), new Color(0.35f, 1f, 0.58f, 1f));

        TMP_Text systemLog = CreateText(monitorBox.transform, "SystemLog", "> READY FOR SIGNAL CALIBRATION", 13f, new Vector2(16f, -118f), new Vector2(458f, 54f), TextAlignmentOptions.TopLeft);
        systemLog.color = new Color(0.45f, 0.75f, 0.85f, 0.9f);

        // Action Buttons (Bottom row)
        Button scanBtn = CreateButton(controlPanel.transform, "ScanButton", "SCAN CHANNELS", buttonSprite, new Vector2(20f, -435f), new Vector2(150f, 48f), 15f);
        Button startSyncBtn = CreateButton(controlPanel.transform, "StartSyncButton", "START SYNC", buttonSprite, new Vector2(185f, -435f), new Vector2(155f, 48f), 15f);
        Button cancelSyncBtn = CreateButton(controlPanel.transform, "CancelSyncButton", "CANCEL", dangerButtonSprite, new Vector2(355f, -435f), new Vector2(155f, 48f), 15f);

        // Close Button on outer frame
        Button closeBtn = CreateButton(panel.transform, "CloseButton", "CLOSE", buttonSprite, new Vector2(980f, -675f), new Vector2(160f, 46f), 16f);

        // Bind references to RelayBUIController
        RelayBUIController ui = canvasObject.GetComponent<RelayBUIController>();
        SerializedObject so = new SerializedObject(ui);
        SetObject(so, "panelRoot", panel);
        SetObject(so, "canvasGroup", canvasObject.GetComponent<CanvasGroup>());
        SetObject(so, "facilityLabel", facility);
        SetObject(so, "relayLabel", relay);
        SetObject(so, "modeLabel", mode);
        SetObject(so, "statusLabel", status);
        SetObject(so, "referenceWaveformRenderer", refRenderer);
        SetObject(so, "currentWaveformRenderer", curRenderer);
        SetObject(so, "referenceSignalLabel", refLabel);
        SetObject(so, "currentSignalLabel", curLabel);

        SerializedProperty btnsProp = so.FindProperty("channelButtons");
        btnsProp.arraySize = 4;
        for (int i = 0; i < 4; i++)
        {
            btnsProp.GetArrayElementAtIndex(i).objectReferenceValue = channelBtns[i];
        }

        SerializedProperty hlsProp = so.FindProperty("channelHighlights");
        hlsProp.arraySize = 4;
        for (int i = 0; i < 4; i++)
        {
            hlsProp.GetArrayElementAtIndex(i).objectReferenceValue = channelHls[i];
        }

        SetObject(so, "frequencySlider", freqSlider.Slider);
        SetObject(so, "frequencyValueText", freqSlider.Value);
        SetObject(so, "phaseSlider", phaseSlider.Slider);
        SetObject(so, "phaseValueText", phaseSlider.Value);
        SetObject(so, "signalMatchText", signalMatch);
        SetObject(so, "frequencyErrorText", freqErr);
        SetObject(so, "phaseErrorText", phaseErr);
        SetObject(so, "progressText", progress);
        SetObject(so, "progressFill", progressFill);
        SetObject(so, "warningBannerText", warningBanner);
        SetObject(so, "systemLogText", systemLog);
        SetObject(so, "scanButton", scanBtn);
        SetObject(so, "startSyncButton", startSyncBtn);
        SetObject(so, "cancelSyncButton", cancelSyncBtn);
        SetObject(so, "closeButton", closeBtn);

        so.ApplyModifiedPropertiesWithoutUndo();

        panel.SetActive(false);
        return ui;
    }

    private static SliderRefs CreateSlider(Transform parent, string name, string label, Vector2 position, float min, float max, string unit)
    {
        GameObject root = new GameObject(name, typeof(RectTransform));
        root.transform.SetParent(parent, false);
        SetAnchored(root, position, new Vector2(490f, 48f), new Vector2(0f, 1f));

        CreateText(root.transform, "Label", label, 16f, new Vector2(0f, 0f), new Vector2(160f, 26f), TextAlignmentOptions.Left);
        Slider slider = CreateSliderControl(root.transform, "Slider", new Vector2(170f, -4f), new Vector2(230f, 22f), min, max);
        TMP_Text value = CreateText(root.transform, "Value", $"0 {unit}", 16f, new Vector2(410f, 0f), new Vector2(80f, 26f), TextAlignmentOptions.Right);

        return new SliderRefs(slider, value);
    }

    private static Slider CreateSliderControl(Transform parent, string name, Vector2 position, Vector2 size, float min, float max)
    {
        GameObject root = new GameObject(name, typeof(RectTransform), typeof(Slider));
        root.transform.SetParent(parent, false);
        SetAnchored(root, position, size, new Vector2(0f, 1f));

        Image background = CreateImage(root.transform, "Background", null, new Color(0.03f, 0.12f, 0.15f, 1f));
        Stretch(background.rectTransform);

        Image fill = CreateImage(root.transform, "Fill", null, new Color(0.35f, 1f, 0.58f, 1f));
        fill.raycastTarget = false;
        RectTransform fillRect = fill.rectTransform;
        fillRect.anchorMin = new Vector2(0f, 0f);
        fillRect.anchorMax = new Vector2(0f, 1f);
        fillRect.pivot = new Vector2(0f, 0.5f);
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;

        Image handle = CreateImage(root.transform, "Handle", null, new Color(0.9f, 1f, 0.95f, 1f));
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
        slider.minValue = min;
        slider.maxValue = max;
        return slider;
    }

    private static Button CreateButton(Transform parent, string name, string text, Sprite sprite, Vector2 position, Vector2 size, float fontSize = 16f)
    {
        GameObject root = CreatePanel(parent, name, sprite, new Color(0.1f, 0.22f, 0.26f, 0.98f));
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
        CreateText(root.transform, "Label", text, fontSize, Vector2.zero, size, TextAlignmentOptions.Center);
        return button;
    }

    private static Image CreateBar(Transform parent, string name, Vector2 position, Vector2 size, Color fillColor)
    {
        GameObject root = CreatePanel(parent, name + "Background", null, new Color(0.02f, 0.08f, 0.1f, 1f));
        SetAnchored(root, position, size, new Vector2(0f, 1f));
        Sprite whiteSprite = GetOrCreateWhiteSprite();
        Image fill = CreateImage(root.transform, name + "Fill", whiteSprite, fillColor);
        Stretch(fill.rectTransform);
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = 0;
        fill.fillAmount = 0f;
        return fill;
    }

    private static Sprite GetOrCreateWhiteSprite()
    {
        string path = "Assets/_Project/UI/white_pixel.png";
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite != null)
        {
            return sprite;
        }

        EnsureFolder("Assets/_Project/UI");
        string fullPath = Path.Combine(Application.dataPath, "_Project/UI/white_pixel.png");
        Texture2D tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
        Color[] cols = new Color[16];
        for (int i = 0; i < 16; i++) cols[i] = Color.white;
        tex.SetPixels(cols);
        tex.Apply();
        File.WriteAllBytes(fullPath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
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
        label.color = new Color(0.85f, 1f, 0.9f, 1f);
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
