using System;
using System.IO;
using System.Linq;
using EchoProtocol.MatchFlow;
using EchoProtocol.RelayA;
using EchoProtocol.RelayB;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class Zone2MissionSetupBuilder
{
    private const string SciFiScenePath = "Assets/Scenes/SciFi.unity";

    [MenuItem("Tools/ECHO Protocol/Setup Zone 2 Mission Flow")]
    public static void SetupZone2MissionFlow()
    {
        string activePath = SceneManager.GetActiveScene().path;
        Scene scene = SceneManager.GetActiveScene();

        if (scene.path != SciFiScenePath && File.Exists(SciFiScenePath))
        {
            scene = EditorSceneManager.OpenScene(SciFiScenePath, OpenSceneMode.Single);
        }

        GameObject zone2Root = GameObject.Find("zone2_GamePlay");
        if (zone2Root == null)
        {
            Debug.LogError("[Zone2Setup] Could not find 'zone2_GamePlay' in scene!");
            return;
        }

        Zone2MissionDirector director = zone2Root.GetComponent<Zone2MissionDirector>();
        if (director == null)
        {
            director = zone2Root.AddComponent<Zone2MissionDirector>();
            Debug.Log("[Zone2Setup] Added Zone2MissionDirector to zone2_GamePlay.");
        }

        SerializedObject so = new SerializedObject(director);

        // 1. Security Terminal
        Transform secTermT = zone2Root.transform.Find("PF_SecurityTerminal_Imported");
        if (secTermT != null)
        {
            SecurityTerminalDownload terminal = secTermT.GetComponent<SecurityTerminalDownload>();
            if (terminal == null) terminal = secTermT.gameObject.AddComponent<SecurityTerminalDownload>();
            so.FindProperty("securityTerminal").objectReferenceValue = terminal;
            Debug.Log("[Zone2Setup] Wired securityTerminal reference.");
        }

        // 2. Relays
        Transform rA1 = zone2Root.transform.Find("RelayA");
        if (rA1 != null)
        {
            so.FindProperty("relayA1").objectReferenceValue = rA1.GetComponent<RelayAController>();
        }

        Transform rA2 = zone2Root.transform.Find("RelayA (1)");
        if (rA2 != null)
        {
            so.FindProperty("relayA2").objectReferenceValue = rA2.GetComponent<RelayAController>();
        }

        Transform rB1 = zone2Root.transform.Find("RelayB");
        if (rB1 != null)
        {
            so.FindProperty("relayB1").objectReferenceValue = rB1.GetComponent<RelayBController>();
        }

        Transform rB2 = zone2Root.transform.Find("RelayB (1)");
        if (rB2 != null)
        {
            so.FindProperty("relayB2").objectReferenceValue = rB2.GetComponent<RelayBController>();
        }

        // 3. Distribution Panels
        Transform p1 = zone2Root.transform.Find("PF_DistributionPanel_Imported");
        if (p1 != null)
        {
            PowerControlUIController ui1 = p1.GetComponentInChildren<PowerControlUIController>(true);
            so.FindProperty("distributionPanel1").objectReferenceValue = ui1;
        }

        Transform p2 = zone2Root.transform.Find("PF_DistributionPanel_Imported (1)");
        if (p2 != null)
        {
            PowerControlUIController ui2 = p2.GetComponentInChildren<PowerControlUIController>(true);
            so.FindProperty("distributionPanel2").objectReferenceValue = ui2;
        }

        // 4. Door Blockers under DoorZone
        GameObject doorZone = GameObject.Find("DoorZone");
        if (doorZone != null)
        {
            Transform door1 = doorZone.transform.Find("Wall BayDoor");
            Transform door2 = doorZone.transform.Find("Wall BayDoor (1)");

            if (door1 != null)
            {
                Transform snap1 = door1.Find("LP_Bay_Door_snaps");
                if (snap1 != null)
                {
                    EnsureDoorBlockerCollider(snap1.gameObject);
                    so.FindProperty("doorBlocker1").objectReferenceValue = snap1.gameObject;
                    Debug.Log("[Zone2Setup] Wired doorBlocker1 reference.");
                }
            }

            if (door2 != null)
            {
                Transform snap2 = door2.Find("LP_Bay_Door_snaps");
                if (snap2 != null)
                {
                    EnsureDoorBlockerCollider(snap2.gameObject);
                    so.FindProperty("doorBlocker2").objectReferenceValue = snap2.gameObject;
                    Debug.Log("[Zone2Setup] Wired doorBlocker2 reference.");
                }
            }
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(director);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[Zone2Setup] Successfully wired Zone 2 Mission references and saved scene!");

        ValidateZone2Setup();

        if (!string.IsNullOrEmpty(activePath) && activePath != SciFiScenePath && File.Exists(activePath))
        {
            EditorSceneManager.OpenScene(activePath, OpenSceneMode.Single);
        }
    }

    private static void EnsureDoorBlockerCollider(GameObject blocker)
    {
        if (blocker == null) return;
        Collider col = blocker.GetComponent<Collider>();
        if (col == null)
        {
            BoxCollider box = blocker.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 4.5f, 3.0f);
            box.size = new Vector3(0.5f, 9.0f, 6.0f);
            box.isTrigger = false;
            EditorUtility.SetDirty(blocker);
            Debug.Log($"[Zone2Setup] Added BoxCollider to {blocker.name} to ensure physical blockage before unlock.");
        }
    }

    [MenuItem("Tools/ECHO Protocol/Validate Zone 2 Mission Setup")]
    public static bool ValidateZone2Setup()
    {
        Zone2MissionDirector director = UnityEngine.Object.FindAnyObjectByType<Zone2MissionDirector>(FindObjectsInactive.Include);
        if (director == null)
        {
            Debug.LogError("[Zone2Validate] Zone2MissionDirector not found in scene!");
            return false;
        }

        bool valid = true;
        if (director.SecurityTerminal == null) { Debug.LogError("[Zone2Validate] SecurityTerminal missing!"); valid = false; }
        if (director.RelayA1 == null) { Debug.LogError("[Zone2Validate] RelayA1 missing!"); valid = false; }
        if (director.RelayA2 == null) { Debug.LogError("[Zone2Validate] RelayA2 missing!"); valid = false; }
        if (director.RelayB1 == null) { Debug.LogError("[Zone2Validate] RelayB1 missing!"); valid = false; }
        if (director.RelayB2 == null) { Debug.LogError("[Zone2Validate] RelayB2 missing!"); valid = false; }
        if (director.DistributionPanel1 == null) { Debug.LogError("[Zone2Validate] DistributionPanel1 missing!"); valid = false; }
        if (director.DistributionPanel2 == null) { Debug.LogError("[Zone2Validate] DistributionPanel2 missing!"); valid = false; }
        if (director.DoorBlocker1 == null) { Debug.LogError("[Zone2Validate] DoorBlocker1 missing!"); valid = false; }
        if (director.DoorBlocker2 == null) { Debug.LogError("[Zone2Validate] DoorBlocker2 missing!"); valid = false; }

        if (director.RelayA1 != null && director.RelayA2 != null && director.RelayA1 == director.RelayA2)
        {
            Debug.LogError("[Zone2Validate] RelayA1 and RelayA2 reference the same instance!");
            valid = false;
        }
        if (director.RelayB1 != null && director.RelayB2 != null && director.RelayB1 == director.RelayB2)
        {
            Debug.LogError("[Zone2Validate] RelayB1 and RelayB2 reference the same instance!");
            valid = false;
        }
        if (director.DistributionPanel1 != null && director.DistributionPanel2 != null && director.DistributionPanel1 == director.DistributionPanel2)
        {
            Debug.LogError("[Zone2Validate] DistributionPanel1 and DistributionPanel2 reference the same instance!");
            valid = false;
        }
        if (director.DoorBlocker1 != null && director.DoorBlocker2 != null && director.DoorBlocker1 == director.DoorBlocker2)
        {
            Debug.LogError("[Zone2Validate] DoorBlocker1 and DoorBlocker2 reference the same instance!");
            valid = false;
        }

        if (valid)
        {
            Debug.Log("[Zone2Validate] ALL 9 Zone 2 Mission references successfully validated!");
        }
        return valid;
    }

    [MenuItem("Tools/ECHO Protocol/Run Zone 2 Flow Tests")]
    public static void RunZone2FlowTests()
    {
        Debug.Log("[Zone2Tests] Running Zone 2 Flow Tests...");
        var testType = typeof(EchoProtocol.Tests.MatchFlow.Zone2MissionFlowTests);
        var instance = Activator.CreateInstance(testType);
        var setUp = testType.GetMethod("SetUp");
        var tearDown = testType.GetMethod("TearDown");
        var methods = testType.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            .Where(m => m.Name.StartsWith("TEST_"))
            .OrderBy(m => m.Name)
            .ToList();

        int passed = 0;
        int failed = 0;
        var sb = new System.Text.StringBuilder();

        foreach (var m in methods)
        {
            try
            {
                setUp?.Invoke(instance, null);
                m.Invoke(instance, null);
                tearDown?.Invoke(instance, null);
                passed++;
                Debug.Log($"[Zone2Tests] PASS: {m.Name}");
            }
            catch (System.Exception ex)
            {
                failed++;
                string err = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                Debug.LogError($"[Zone2Tests] FAIL: {m.Name} -> {err}");
                sb.AppendLine($"FAILED: {m.Name} -> {err}");
            }
        }

        string summary = $"status={(failed == 0 ? "PASS" : "FAIL")} passed={passed} failed={failed} skipped=0 duration=0.000s";
        Debug.Log($"[Zone2Tests] Result: {summary}");
        try
        {
            string outPath = Path.Combine(Application.dataPath, "../test_results.txt");
            File.WriteAllText(outPath, summary + "\n" + sb.ToString());
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning("[Zone2Tests] Could not write test_results.txt: " + ex.Message);
        }
    }
}
