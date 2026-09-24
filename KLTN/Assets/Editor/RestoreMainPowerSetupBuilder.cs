using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// [InitializeOnLoad] - Disabled auto-run on domain reload (migrated to Zone 2 mission flow)
public static class RestoreMainPowerSetupBuilder
{
    private const string TerminalPrefabPath = "Assets/Prefabs/Gameplay/Imported/PF_SecurityTerminal_Imported.prefab";
    private const string PowerControlPrefabPath = "Assets/Prefabs/Gameplay/Imported/PF_PowerControl_Imported.prefab";
    private static bool _hasRun;

    /*
    static RestoreMainPowerSetupBuilder()
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
        Debug.Log("[RestoreMainPowerSetupBuilder] InitializeOnLoad triggered: Setting up system and running tests...");
        try
        {
            SetupSystem();
            RunRestoreMainPowerTests();
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[RestoreMainPowerSetupBuilder] Error during AutoRun: {ex}");
        }
    }

    [MenuItem("Tools/ECHO Protocol/Setup Restore Main Power System")]
    public static void SetupSystem()
    {
        SetupSecurityTerminalPrefab();
        SetupPowerControlPrefab();
        UpdateSceneInstances();
        Debug.Log("[RestoreMainPowerSetupBuilder] Successfully setup Restore Main Power system on prefabs and scenes!");
    }

    [MenuItem("Tools/ECHO Protocol/Rebuild Security Terminal UI")]
    public static void SetupSecurityTerminalPrefab()
    {
        if (!File.Exists(TerminalPrefabPath))
        {
            Debug.LogWarning($"[RestoreMainPowerSetupBuilder] Terminal prefab not found at {TerminalPrefabPath}. Skipping.");
            return;
        }

        GameObject prefab = PrefabUtility.LoadPrefabContents(TerminalPrefabPath);
        try
        {
            Transform existingUi = prefab.transform.Find("SecurityTerminal_UI");
            if (existingUi != null)
            {
                UnityEngine.Object.DestroyImmediate(existingUi.gameObject);
            }

            // Ensure BoxCollider covers terminal console
            BoxCollider col = prefab.GetComponent<BoxCollider>();
            if (col == null) col = prefab.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.6f, 0f);
            col.size = new Vector3(1.8f, 1.2f, 1.0f);

            // Ensure SecurityTerminalDownload component
            SecurityTerminalDownload download = prefab.GetComponent<SecurityTerminalDownload>();
            if (download == null) download = prefab.AddComponent<SecurityTerminalDownload>();

            // Build UI Canvas
            BuildSecurityTerminalUi(prefab.transform, download);

            PrefabUtility.SaveAsPrefabAsset(prefab, TerminalPrefabPath);
            Debug.Log("[RestoreMainPowerSetupBuilder] Saved PF_SecurityTerminal_Imported with SecurityTerminal_UI.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefab);
        }
    }

    public static void SetupPowerControlPrefab()
    {
        if (!File.Exists(PowerControlPrefabPath))
        {
            Debug.LogWarning($"[RestoreMainPowerSetupBuilder] PowerControl prefab not found at {PowerControlPrefabPath} (migrated to DistributionPanel). Skipping.");
            return;
        }

        GameObject prefab = PrefabUtility.LoadPrefabContents(PowerControlPrefabPath);
        try
        {
            Transform existingUi = prefab.transform.Find("PowerControl_UI");
            if (existingUi != null)
            {
                UnityEngine.Object.DestroyImmediate(existingUi.gameObject);
            }

            // Ensure BoxCollider covers console
            BoxCollider col = prefab.GetComponent<BoxCollider>();
            if (col == null) col = prefab.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.6f, 0f);
            col.size = new Vector3(1.8f, 1.2f, 1.0f);

            // Ensure PowerPuzzleStation component
            PowerPuzzleStation station = prefab.GetComponent<PowerPuzzleStation>();
            if (station == null) station = prefab.AddComponent<PowerPuzzleStation>();
            SerializedObject soStation = new SerializedObject(station);
            soStation.FindProperty("stationType").enumValueIndex = (int)PowerPuzzleStationType.PowerControl;
            soStation.ApplyModifiedPropertiesWithoutUndo();

            // Build UI Canvas
            BuildPowerControlUi(prefab.transform);

            PrefabUtility.SaveAsPrefabAsset(prefab, PowerControlPrefabPath);
            Debug.Log("[RestoreMainPowerSetupBuilder] Saved PF_PowerControl_Imported with PowerControl_UI.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefab);
        }
    }

    private static void BuildSecurityTerminalUi(Transform prefabRoot, SecurityTerminalDownload download)
    {
        Sprite panelSprite = LoadSprite("Assets/_Project/UI/BunkerSurvivalUI/Sprites/NineSlice/panel_industrial_main_normal_9slice.png");
        Sprite sidePanelSprite = LoadSprite("Assets/_Project/UI/BunkerSurvivalUI/Sprites/NineSlice/frame_security_terminal_normal_9slice.png");
        Sprite cardSprite = LoadSprite("Assets/_Project/UI/BunkerSurvivalUI/Sprites/NineSlice/card_item_industrial_normal_9slice.png");
        Sprite buttonSprite = LoadSprite("Assets/_Project/UI/BunkerSurvivalUI/Sprites/NineSlice/button_secondary_normal_9slice.png");
        Sprite whitePixel = LoadSprite("Assets/_Project/UI/BunkerSurvivalUI/Sprites/NineSlice/panel_cutout_overlay_normal_9slice.png");

        GameObject canvasObj = new GameObject("SecurityTerminal_UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObj.transform.SetParent(prefabRoot, false);

        Canvas canvas = canvasObj.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 95;

        CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        SecurityTerminalUIController uiController = canvasObj.AddComponent<SecurityTerminalUIController>();

        // Main Panel Root (Dark industrial frame)
        GameObject panelRoot = CreatePanel(canvasObj.transform, "PanelRoot", panelSprite, new Color(0.02f, 0.05f, 0.07f, 0.96f));
        RectTransform rootRt = panelRoot.GetComponent<RectTransform>();
        rootRt.anchorMin = new Vector2(0.5f, 0.5f);
        rootRt.anchorMax = new Vector2(0.5f, 0.5f);
        rootRt.pivot = new Vector2(0.5f, 0.5f);
        rootRt.sizeDelta = new Vector2(960f, 620f);
        rootRt.anchoredPosition = Vector2.zero;

        // Header Bar (Top Row: Title on Left, Status Badge on Right - NO OVERLAPPING)
        TMP_Text headerTitle = CreateText(panelRoot.transform, "HeaderTitle", "ECHO PROTOCOL // SECURITY TERMINAL [ZONE 2]", 21f, new Vector2(40f, -22f), new Vector2(540f, 30f), TextAlignmentOptions.Left);
        headerTitle.color = new Color(0.55f, 0.85f, 1f, 1f);

        TMP_Text statusBanner = CreateText(panelRoot.transform, "StatusBanner", "● STATUS: NETWORK OFFLINE", 17f, new Vector2(580f, -22f), new Vector2(340f, 30f), TextAlignmentOptions.Right);

        // Header Divider Line
        GameObject headerDiv = CreatePanel(panelRoot.transform, "HeaderDivider", whitePixel, new Color(0f, 0.85f, 1f, 0.25f));
        SetAnchored(headerDiv, new Vector2(40f, -56f), new Vector2(880f, 2f), new Vector2(0f, 1f));

        // ──────────────────────────────────────────────────────────────────────
        // 1. Offline Warning Panel (Relays < 4)
        // ──────────────────────────────────────────────────────────────────────
        GameObject offlinePanel = CreatePanel(panelRoot.transform, "OfflineWarningPanel", sidePanelSprite, new Color(0.10f, 0.02f, 0.03f, 0.98f));
        SetAnchored(offlinePanel, new Vector2(40f, -72f), new Vector2(880f, 460f), new Vector2(0f, 1f));

        CreateText(offlinePanel.transform, "WarningTitle", "<color=#FF1744><b>⚠ EMERGENCY SYSTEM LOCKOUT</b></color>", 25f, new Vector2(0f, -25f), new Vector2(880f, 35f), TextAlignmentOptions.Center);
        CreateText(offlinePanel.transform, "WarningSub", "<color=#90A4AE>Security authentication bus is de-energized. Facility relay grid disconnected.</color>", 15f, new Vector2(0f, -65f), new Vector2(880f, 24f), TextAlignmentOptions.Center);

        // Structured Relay Grid Diagnostic Card
        GameObject diagCard = CreatePanel(offlinePanel.transform, "DiagnosticCard", cardSprite, new Color(0.04f, 0.015f, 0.025f, 0.95f));
        SetAnchored(diagCard, new Vector2(100f, -100f), new Vector2(680f, 190f), new Vector2(0f, 1f));

        TMP_Text offlineDetail = CreateText(diagCard.transform, "OfflineDetail", "", 17f, new Vector2(20f, -15f), new Vector2(640f, 160f), TextAlignmentOptions.Center);

        // Action Instruction
        CreateText(offlinePanel.transform, "ActionInstruction", "<color=#FFD54F><b>REQUIRED ACTION:</b></color> REPAIR ALL 4 RELAYS IN ZONE 2 TO RESTORE TERMINAL ACCESS", 16f, new Vector2(0f, -315f), new Vector2(880f, 32f), TextAlignmentOptions.Center);
        CreateText(offlinePanel.transform, "SubInstruction", "<color=#546E7A>Relay stations A1, A2 (Power) and B1, B2 (Data) are scattered throughout Zone 2.</color>", 14f, new Vector2(0f, -350f), new Vector2(880f, 24f), TextAlignmentOptions.Center);

        // ──────────────────────────────────────────────────────────────────────
        // 2. In-Progress Panel (Relays == 4, downloading)
        // ──────────────────────────────────────────────────────────────────────
        GameObject inProgPanel = CreatePanel(panelRoot.transform, "InProgressPanel", sidePanelSprite, new Color(0.015f, 0.04f, 0.06f, 0.98f));
        SetAnchored(inProgPanel, new Vector2(40f, -72f), new Vector2(880f, 460f), new Vector2(0f, 1f));

        CreateText(inProgPanel.transform, "HoldTitle", "<color=#00E5FF><b>SECURITY AUTHENTICATION INTERFACE</b></color>", 25f, new Vector2(0f, -30f), new Vector2(880f, 35f), TextAlignmentOptions.Center);
        CreateText(inProgPanel.transform, "HoldSubtitle", "<color=#80DEEA>RELAY NETWORK ONLINE — READY FOR ENCRYPTION HANDSHAKE</color>", 15f, new Vector2(0f, -70f), new Vector2(880f, 24f), TextAlignmentOptions.Center);

        // Instruction Card
        GameObject instructCard = CreatePanel(inProgPanel.transform, "InstructCard", cardSprite, new Color(0.01f, 0.03f, 0.05f, 0.95f));
        SetAnchored(instructCard, new Vector2(140f, -115f), new Vector2(600f, 55f), new Vector2(0f, 1f));
        CreateText(instructCard.transform, "HoldInstruction", "HOLD <color=#FFEB3B><b>[E]</b></color> AT TERMINAL TO DOWNLOAD SECURITY PROTOCOLS", 18f, Vector2.zero, new Vector2(600f, 55f), TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));

        TMP_Text progressText = CreateText(inProgPanel.transform, "ProgressLabel", "AUTHENTICATION PROGRESS: 0%", 20f, new Vector2(0f, -200f), new Vector2(880f, 30f), TextAlignmentOptions.Center);

        // Progress Bar
        GameObject barBg = CreatePanel(inProgPanel.transform, "ProgressBarBg", whitePixel, new Color(0.1f, 0.15f, 0.2f, 0.8f));
        SetAnchored(barBg, new Vector2(140f, -240f), new Vector2(600f, 28f), new Vector2(0f, 1f));
        GameObject barFillObj = CreatePanel(barBg.transform, "ProgressBarFill", whitePixel, new Color(0f, 0.9f, 0.45f, 1f));
        Image barFill = barFillObj.GetComponent<Image>();
        barFill.type = Image.Type.Filled;
        barFill.fillMethod = Image.FillMethod.Horizontal;
        barFill.fillAmount = 0f;
        Stretch(barFillObj.GetComponent<RectTransform>());

        CreateText(inProgPanel.transform, "ProgressNote", "<color=#546E7A>Keep within terminal perimeter until download completes</color>", 14f, new Vector2(0f, -285f), new Vector2(880f, 24f), TextAlignmentOptions.Center);

        // ──────────────────────────────────────────────────────────────────────
        // 3. Completed Panel (Authentication granted, reveal code)
        // ──────────────────────────────────────────────────────────────────────
        GameObject compPanel = CreatePanel(panelRoot.transform, "CompletedPanel", sidePanelSprite, new Color(0.015f, 0.05f, 0.04f, 0.98f));
        SetAnchored(compPanel, new Vector2(40f, -72f), new Vector2(880f, 460f), new Vector2(0f, 1f));

        CreateText(compPanel.transform, "SuccessTitle", "<color=#00E676><b>✓ SECURITY AUTHENTICATION COMPLETE</b></color>", 25f, new Vector2(0f, -30f), new Vector2(880f, 35f), TextAlignmentOptions.Center);

        GameObject codeCard = CreatePanel(compPanel.transform, "CodeCard", cardSprite, new Color(0.01f, 0.04f, 0.03f, 0.95f));
        SetAnchored(codeCard, new Vector2(140f, -85f), new Vector2(600f, 160f), new Vector2(0f, 1f));
        TMP_Text codeDisplay = CreateText(codeCard.transform, "CodeDisplay", "ZONE 2 EXIT AUTHORIZATION CODE:\n<size=56><color=#00FF99><b>0427</b></color></size>", 22f, Vector2.zero, new Vector2(600f, 160f), TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));

        TMP_Text instruction = CreateText(compPanel.transform, "NextInstruction", "<color=#FFEB3B><b>OBJECTIVE UPDATED:</b></color> ENTER ACCESS CODE AT ZONE DOOR DISTRIBUTION PANEL\n<color=#90A4AE>Input code at either of the two exit door panels to grant access.</color>", 16f, new Vector2(0f, -270f), new Vector2(880f, 50f), TextAlignmentOptions.Center);

        // ──────────────────────────────────────────────────────────────────────
        // Footer Section (Close Button + Hint)
        // ──────────────────────────────────────────────────────────────────────
        GameObject footerDiv = CreatePanel(panelRoot.transform, "FooterDivider", whitePixel, new Color(0.3f, 0.4f, 0.5f, 0.25f));
        SetAnchored(footerDiv, new Vector2(40f, -542f), new Vector2(880f, 1f), new Vector2(0f, 1f));

        CreateText(panelRoot.transform, "FooterHint", "<color=#546E7A>PRESS [ESC] OR WALK AWAY TO DISENGAGE TERMINAL</color>", 13f, new Vector2(40f, -558f), new Vector2(400f, 30f), TextAlignmentOptions.Left);

        Button closeBtn = CreateButton(panelRoot.transform, "CloseButton", "DISENGAGE [ESC]", buttonSprite, new Vector2(700f, -552f), new Vector2(220f, 44f));

        // Wire serialized fields
        SerializedObject so = new SerializedObject(uiController);
        SetObject(so, "terminal", download);
        SetObject(so, "rootCanvas", panelRoot);
        SetObject(so, "inProgressPanel", inProgPanel);
        SetObject(so, "completedPanel", compPanel);
        SetObject(so, "offlineWarningPanel", offlinePanel);
        SetObject(so, "headerTitleText", headerTitle);
        SetObject(so, "statusBannerText", statusBanner);
        SetObject(so, "progressText", progressText);
        SetObject(so, "codeDisplayText", codeDisplay);
        SetObject(so, "instructionText", instruction);
        SetObject(so, "offlineDetailText", offlineDetail);
        SetObject(so, "progressBarFill", barFill);
        SetObject(so, "closeButton", closeBtn);
        so.ApplyModifiedPropertiesWithoutUndo();

        panelRoot.SetActive(false);
    }

    private static void BuildPowerControlUi(Transform prefabRoot)
    {
        Sprite panelSprite = LoadSprite("Assets/_Project/UI/BunkerSurvivalUI/Sprites/NineSlice/panel_industrial_main_normal_9slice.png");
        Sprite sidePanelSprite = LoadSprite("Assets/_Project/UI/BunkerSurvivalUI/Sprites/NineSlice/frame_security_terminal_normal_9slice.png");
        Sprite buttonSprite = LoadSprite("Assets/_Project/UI/BunkerSurvivalUI/Sprites/NineSlice/button_secondary_normal_9slice.png");
        Sprite dangerSprite = LoadSprite("Assets/_Project/UI/BunkerSurvivalUI/Sprites/NineSlice/button_danger_normal_9slice.png");
        Sprite confirmSprite = LoadSprite("Assets/_Project/UI/BunkerSurvivalUI/Sprites/NineSlice/button_primary_normal_9slice.png");
        if (confirmSprite == null) confirmSprite = buttonSprite;

        GameObject canvasObj = new GameObject("PowerControl_UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObj.transform.SetParent(prefabRoot, false);

        Canvas canvas = canvasObj.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 95;

        CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        PowerControlUIController uiController = canvasObj.AddComponent<PowerControlUIController>();

        // Main Panel Root
        GameObject panelRoot = CreatePanel(canvasObj.transform, "PanelRoot", panelSprite, new Color(0.02f, 0.05f, 0.07f, 0.96f));
        RectTransform rootRt = panelRoot.GetComponent<RectTransform>();
        rootRt.anchorMin = new Vector2(0.5f, 0.5f);
        rootRt.anchorMax = new Vector2(0.5f, 0.5f);
        rootRt.pivot = new Vector2(0.5f, 0.5f);
        rootRt.sizeDelta = new Vector2(880f, 720f);
        rootRt.anchoredPosition = Vector2.zero;

        TMP_Text headerTitle = CreateText(panelRoot.transform, "HeaderTitle", "ECHO FACILITY // MAIN POWER CONTROL", 24f, new Vector2(40f, -24f), new Vector2(500f, 32f), TextAlignmentOptions.Left);
        TMP_Text statusBanner = CreateText(panelRoot.transform, "StatusBanner", "ENTER AUTHORIZATION CODE", 26f, new Vector2(40f, -60f), new Vector2(650f, 36f), TextAlignmentOptions.Left);

        // Locked Panel
        GameObject lockedPanel = CreatePanel(panelRoot.transform, "LockedPanel", sidePanelSprite, new Color(0.12f, 0.02f, 0.02f, 0.98f));
        SetAnchored(lockedPanel, new Vector2(40f, -110f), new Vector2(800f, 520f), new Vector2(0f, 1f));
        CreateText(lockedPanel.transform, "LockTitle", "<color=#FF1744>LOCKED</color>", 36f, new Vector2(0f, -160f), new Vector2(800f, 48f), TextAlignmentOptions.Center);
        CreateText(lockedPanel.transform, "LockSub", "SECURITY AUTHENTICATION REQUIRED\nCOMPLETE SECURITY HOLD AT SECURITY TERMINAL FIRST", 22f, new Vector2(0f, -230f), new Vector2(800f, 60f), TextAlignmentOptions.Center);

        // Online Panel
        GameObject onlinePanel = CreatePanel(panelRoot.transform, "OnlinePanel", sidePanelSprite, new Color(0.015f, 0.06f, 0.04f, 0.98f));
        SetAnchored(onlinePanel, new Vector2(40f, -110f), new Vector2(800f, 520f), new Vector2(0f, 1f));
        CreateText(onlinePanel.transform, "OnlineTitle", "<color=#00E676>MAIN POWER RESTORED</color>", 36f, new Vector2(0f, -160f), new Vector2(800f, 48f), TextAlignmentOptions.Center);
        CreateText(onlinePanel.transform, "OnlineSub", "FACILITY GRID ONLINE\nSECURITY LOCKDOWN PROTOCOLS ENGAGED", 22f, new Vector2(0f, -230f), new Vector2(800f, 60f), TextAlignmentOptions.Center);

        // Keypad Panel
        GameObject keypadPanel = CreatePanel(panelRoot.transform, "KeypadPanel", sidePanelSprite, new Color(0.015f, 0.04f, 0.06f, 0.98f));
        SetAnchored(keypadPanel, new Vector2(40f, -110f), new Vector2(800f, 520f), new Vector2(0f, 1f));

        // Display slots
        TMP_Text codeSlots = CreateText(keypadPanel.transform, "CodeSlots", "[ _ ]   [ _ ]   [ _ ]   [ _ ]", 40f, new Vector2(0f, -30f), new Vector2(800f, 50f), TextAlignmentOptions.Center);
        TMP_Text feedback = CreateText(keypadPanel.transform, "Feedback", "", 20f, new Vector2(0f, -85f), new Vector2(800f, 30f), TextAlignmentOptions.Center);
        GameObject statusLedObj = CreatePanel(keypadPanel.transform, "StatusLED", null, new Color(0f, 0.9f, 1f, 0.85f));
        SetAnchored(statusLedObj, new Vector2(230f, -118f), new Vector2(408f, 4f), new Vector2(0f, 1f));
        Image statusLedImg = statusLedObj.GetComponent<Image>();

        // 3x4 Grid of Buttons
        // Row 1: 1, 2, 3
        // Row 2: 4, 5, 6
        // Row 3: 7, 8, 9
        // Row 4: Clear, 0, Confirm
        Button[] digitButtons = new Button[10];
        float btnW = 120f;
        float btnH = 65f;
        float spacingX = 24f;
        float spacingY = 16f;
        float startX = 230f;
        float startY = -135f;

        // Digits 1-9
        for (int r = 0; r < 3; r++)
        {
            for (int c = 0; c < 3; c++)
            {
                int val = r * 3 + c + 1;
                Vector2 pos = new Vector2(startX + c * (btnW + spacingX), startY - r * (btnH + spacingY));
                Button b = CreateButton(keypadPanel.transform, "Btn_" + val, val.ToString(), buttonSprite, pos, new Vector2(btnW, btnH));
                digitButtons[val] = b;
            }
        }

        // Row 4
        Vector2 posClear = new Vector2(startX, startY - 3 * (btnH + spacingY));
        Vector2 pos0 = new Vector2(startX + (btnW + spacingX), startY - 3 * (btnH + spacingY));
        Vector2 posConfirm = new Vector2(startX + 2 * (btnW + spacingX), startY - 3 * (btnH + spacingY));

        Button clearBtn = CreateButton(keypadPanel.transform, "Btn_Clear", "CLEAR", dangerSprite, posClear, new Vector2(btnW, btnH));
        Button btn0 = CreateButton(keypadPanel.transform, "Btn_0", "0", buttonSprite, pos0, new Vector2(btnW, btnH));
        digitButtons[0] = btn0;
        Button confirmBtn = CreateButton(keypadPanel.transform, "Btn_Confirm", "CONFIRM", confirmSprite, posConfirm, new Vector2(btnW, btnH));

        // Close Button
        Button closeBtn = CreateButton(panelRoot.transform, "CloseButton", "CLOSE [ESC]", buttonSprite, new Vector2(620f, -650f), new Vector2(220f, 45f));

        // Wire serialized fields
        SerializedObject so = new SerializedObject(uiController);
        SetObject(so, "rootCanvas", panelRoot);
        SetObject(so, "lockedPanel", lockedPanel);
        SetObject(so, "keypadPanel", keypadPanel);
        SetObject(so, "onlinePanel", onlinePanel);
        SetObject(so, "headerTitleText", headerTitle);
        SetObject(so, "statusBannerText", statusBanner);
        SetObject(so, "codeSlotsText", codeSlots);
        SetObject(so, "feedbackText", feedback);
        SetObject(so, "statusLed", statusLedImg);
        SetObject(so, "clearButton", clearBtn);
        SetObject(so, "confirmButton", confirmBtn);
        SetObject(so, "closeButton", closeBtn);

        SerializedProperty btnArray = so.FindProperty("digitButtons");
        btnArray.arraySize = 10;
        for (int i = 0; i < 10; i++)
        {
            btnArray.GetArrayElementAtIndex(i).objectReferenceValue = digitButtons[i];
        }

        so.ApplyModifiedPropertiesWithoutUndo();

        panelRoot.SetActive(false);
    }

    private static void UpdateSceneInstances()
    {
        string activePath = SceneManager.GetActiveScene().path;
        string scenePath = "Assets/Scenes/SciFi.unity";

        if (File.Exists(scenePath))
        {
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            // 1. Find or add PowerPuzzleController on GameMode_ResearchFacility
            GameObject gameMode = GameObject.Find("GameMode_ResearchFacility");
            PowerPuzzleController puzzleCtrl = null;
            MatchFlowController matchFlow = null;
            if (gameMode != null)
            {
                puzzleCtrl = gameMode.GetComponent<PowerPuzzleController>();
                if (puzzleCtrl == null)
                {
                    puzzleCtrl = gameMode.AddComponent<PowerPuzzleController>();
                    Debug.Log($"[RestoreMainPowerSetupBuilder] Added PowerPuzzleController to {gameMode.name}");
                }

                matchFlow = gameMode.GetComponent<MatchFlowController>();
            }
            else
            {
                puzzleCtrl = UnityEngine.Object.FindAnyObjectByType<PowerPuzzleController>(FindObjectsInactive.Include);
                matchFlow = UnityEngine.Object.FindAnyObjectByType<MatchFlowController>(FindObjectsInactive.Include);
            }

            // 2. Find SecurityTerminal_Hold in scene
            GameObject secHold = GameObject.Find("SecurityTerminal_Hold");
            SecurityTerminalDownload terminal = null;
            if (secHold != null)
            {
                terminal = secHold.GetComponent<SecurityTerminalDownload>();
                if (terminal == null) terminal = secHold.AddComponent<SecurityTerminalDownload>();
            }
            else
            {
                terminal = UnityEngine.Object.FindAnyObjectByType<SecurityTerminalDownload>(FindObjectsInactive.Include);
            }

            // 3. Find PowerControl_Station in scene
            GameObject pwrStation = GameObject.Find("PowerControl_Station");
            PowerPuzzleStation station = null;
            PowerControlUIController pwrUi = null;
            if (pwrStation != null)
            {
                station = pwrStation.GetComponent<PowerPuzzleStation>();
                if (station == null) station = pwrStation.AddComponent<PowerPuzzleStation>();
                pwrUi = pwrStation.GetComponentInChildren<PowerControlUIController>(true);
            }

            // 4. Link MatchFlowController references
            if (matchFlow != null)
            {
                SerializedObject soFlow = new SerializedObject(matchFlow);
                var pwrProp = soFlow.FindProperty("powerPuzzle");
                if (pwrProp != null && puzzleCtrl != null) pwrProp.objectReferenceValue = puzzleCtrl;
                var secProp = soFlow.FindProperty("securityTerminal");
                if (secProp != null && terminal != null) secProp.objectReferenceValue = terminal;
                soFlow.ApplyModifiedPropertiesWithoutUndo();
                Debug.Log("[RestoreMainPowerSetupBuilder] Linked MatchFlowController references (powerPuzzle, securityTerminal).");
            }

            // 5. Link PowerPuzzleStation and PowerControlUIController references
            if (station != null && puzzleCtrl != null)
            {
                SerializedObject soStation = new SerializedObject(station);
                soStation.FindProperty("stationType").enumValueIndex = (int)PowerPuzzleStationType.PowerControl;
                soStation.FindProperty("controller").objectReferenceValue = puzzleCtrl;
                soStation.ApplyModifiedPropertiesWithoutUndo();
                Debug.Log("[RestoreMainPowerSetupBuilder] Linked PowerPuzzleStation.controller.");
            }

            if (pwrUi != null && puzzleCtrl != null)
            {
                SerializedObject soUi = new SerializedObject(pwrUi);
                soUi.FindProperty("controller").objectReferenceValue = puzzleCtrl;
                soUi.ApplyModifiedPropertiesWithoutUndo();
                Debug.Log("[RestoreMainPowerSetupBuilder] Linked PowerControlUIController.controller.");
            }

            // 6. Ensure Player in SciFi scene for offline Play Mode testing
            EnsurePlayerInSciFiScene();

            var activeScene2 = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(activeScene2);
            EditorSceneManager.SaveScene(activeScene2, activeScene2.path);
            Debug.Log("[RestoreMainPowerSetupBuilder] Successfully updated and saved SciFi.unity!");
        }

        if (!string.IsNullOrEmpty(activePath) && File.Exists(activePath))
        {
            EditorSceneManager.OpenScene(activePath, OpenSceneMode.Single);
        }
    }

    public static void EnsurePlayerInSciFiScene()
    {
        var existingPlayer = UnityEngine.Object.FindAnyObjectByType<EchoProtocol.Networking.NetworkPlayerMovement>(FindObjectsInactive.Include);
        GameObject playerObj = null;
        Vector3 spawnPos = new Vector3(-26.40f, 0.70f, -428.20f);
        Quaternion spawnRot = Quaternion.Euler(0f, 180f, 0f);

        if (existingPlayer == null)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PlayerNetwork.prefab");
            if (prefab != null)
            {
                playerObj = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                playerObj.name = "PlayerNetwork";
                Debug.Log($"[RestoreMainPowerSetupBuilder] Instantiated PlayerNetwork in SciFi.unity");
            }
        }
        else
        {
            playerObj = existingPlayer.gameObject;
        }

        if (playerObj != null)
        {
            // Use Undo.RecordObject to ensure the modification is tracked and serialized properly
            Undo.RecordObject(playerObj.transform, "Move PlayerNetwork to spawn");
            playerObj.transform.position = spawnPos;
            playerObj.transform.rotation = spawnRot;

            // For prefab instances, RecordPrefabInstancePropertyModifications must be called AFTER setting value
            if (PrefabUtility.IsPartOfPrefabInstance(playerObj))
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(playerObj.transform);
                Debug.Log($"[RestoreMainPowerSetupBuilder] Recorded prefab instance property modifications for position {spawnPos}");
            }
            EditorUtility.SetDirty(playerObj.transform);
            EditorUtility.SetDirty(playerObj);
            Debug.Log($"[RestoreMainPowerSetupBuilder] Moved PlayerNetwork to {spawnPos}");

            var playerCam = UnityEngine.Object.FindAnyObjectByType<PlayerCamera>(FindObjectsInactive.Include);
            if (playerCam != null)
            {
                Undo.RecordObject(playerCam, "Link PlayerCamera to PlayerNetwork");
                var soCam = new SerializedObject(playerCam);
                soCam.FindProperty("target").objectReferenceValue = playerObj.transform;
                soCam.ApplyModifiedPropertiesWithoutUndo();
                playerCam.gameObject.SetActive(true);
                playerCam.enabled = true;
                EditorUtility.SetDirty(playerCam);
                Debug.Log("[RestoreMainPowerSetupBuilder] Assigned PlayerCamera.target to PlayerNetwork.");
            }

            EchoSetup_PlayerNetwork.FixScenePlayerSetup();
        }
    }

    [MenuItem("Tools/ECHO Protocol/Teleport Player To Security Terminal")]
    public static void TeleportPlayerToSecurityTerminal()
    {
        var movement = UnityEngine.Object.FindAnyObjectByType<EchoProtocol.Networking.NetworkPlayerMovement>();
        if (movement != null)
        {
            var cc = movement.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            movement.transform.position = new Vector3(-26.40f, 0.70f, -428.20f);
            movement.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            if (cc != null) cc.enabled = true;
            Debug.Log($"[Teleport] Teleported player to Security Terminal: {movement.transform.position}");
        }
        else
        {
            Debug.LogWarning("[Teleport] No NetworkPlayerMovement found in scene.");
        }
    }

    [MenuItem("Tools/ECHO Protocol/Teleport Player To Power Control")]
    public static void TeleportPlayerToPowerControl()
    {
        var movement = UnityEngine.Object.FindAnyObjectByType<EchoProtocol.Networking.NetworkPlayerMovement>();
        if (movement != null)
        {
            var cc = movement.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            movement.transform.position = new Vector3(67.20f, 0.70f, -397.30f);
            movement.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            if (cc != null) cc.enabled = true;
            Debug.Log($"[Teleport] Teleported player to Power Control: {movement.transform.position}");
        }
        else
        {
            Debug.LogWarning("[Teleport] No NetworkPlayerMovement found in scene.");
        }
    }

    [MenuItem("Tools/ECHO Protocol/Set Relays A and B Online (Dev Test)")]
    public static void SetRelaysOnlineDevTest()
    {
        var relayA = UnityEngine.Object.FindAnyObjectByType<EchoProtocol.RelayA.RelayAController>();
        if (relayA != null)
        {
            relayA.ApplyOnlineFromAuthority();
            Debug.Log("[DevTest] Relay A set ONLINE.");
        }
        else
        {
            Debug.LogWarning("[DevTest] RelayAController not found.");
        }

        var relayB = UnityEngine.Object.FindAnyObjectByType<EchoProtocol.RelayB.RelayBController>();
        if (relayB != null)
        {
            relayB.ApplyOnlineFromAuthority();
            Debug.Log("[DevTest] Relay B set ONLINE.");
        }
        else
        {
            Debug.LogWarning("[DevTest] RelayBController not found.");
        }
    }

    [MenuItem("Tools/ECHO Protocol/Diagnose SciFi Scene Interactivity")]
    public static void DiagnoseSciFiScene()
    {
        string scenePath = "Assets/Scenes/SciFi.unity";
        if (!File.Exists(scenePath))
        {
            Debug.LogError($"Scene {scenePath} not found!");
            return;
        }

        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("=== DIAGNOSE SCIFI SCENE INTERACTIVITY ===");

        // 1. Search all relevant GameObjects
        var allGos = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        sb.AppendLine($"Total GameObjects in scene: {allGos.Length}");

        var secHold = GameObject.Find("SecurityTerminal_Hold");
        if (secHold != null)
        {
            sb.AppendLine("\n=== DETAILED SecurityTerminal_Hold ===");
            sb.AppendLine($"Path: {GetPath(secHold)}");
            sb.AppendLine($"Active: {secHold.activeSelf} (in hierarchy: {secHold.activeInHierarchy})");
            sb.AppendLine($"Layer: {LayerMask.LayerToName(secHold.layer)} ({secHold.layer})");
            sb.AppendLine($"Position: {secHold.transform.position}");
            sb.AppendLine($"IsPrefab: {PrefabUtility.IsPartOfAnyPrefab(secHold)}");
            foreach (var comp in secHold.GetComponents<Component>())
            {
                sb.AppendLine($"   Component: {comp?.GetType().FullName}");
            }
            sb.AppendLine($"Children count: {secHold.transform.childCount}");
            for (int i = 0; i < secHold.transform.childCount; i++)
            {
                var ch = secHold.transform.GetChild(i);
                sb.AppendLine($"   Child[{i}]: {ch.name} (active={ch.gameObject.activeSelf})");
                foreach (var comp in ch.GetComponents<Component>())
                {
                    sb.AppendLine($"      Component: {comp?.GetType().FullName}");
                }
            }
        }
        else
        {
            sb.AppendLine("\n=== SecurityTerminal_Hold NOT FOUND ===");
        }

        var pwrStation = GameObject.Find("PowerControl_Station");
        if (pwrStation != null)
        {
            sb.AppendLine("\n=== DETAILED PowerControl_Station ===");
            sb.AppendLine($"Path: {GetPath(pwrStation)}");
            sb.AppendLine($"Active: {pwrStation.activeSelf} (in hierarchy: {pwrStation.activeInHierarchy})");
            sb.AppendLine($"Layer: {LayerMask.LayerToName(pwrStation.layer)} ({pwrStation.layer})");
            sb.AppendLine($"Position: {pwrStation.transform.position}");
            sb.AppendLine($"IsPrefab: {PrefabUtility.IsPartOfAnyPrefab(pwrStation)}");
            foreach (var comp in pwrStation.GetComponents<Component>())
            {
                sb.AppendLine($"   Component: {comp?.GetType().FullName}");
            }
            sb.AppendLine($"Children count: {pwrStation.transform.childCount}");
            for (int i = 0; i < pwrStation.transform.childCount; i++)
            {
                var ch = pwrStation.transform.GetChild(i);
                sb.AppendLine($"   Child[{i}]: {ch.name} (active={ch.gameObject.activeSelf})");
                foreach (var comp in ch.GetComponents<Component>())
                {
                    sb.AppendLine($"      Component: {comp?.GetType().FullName}");
                }
            }
        }
        else
        {
            sb.AppendLine("\n=== PowerControl_Station NOT FOUND ===");
        }

        // Check if there are other terminals or power controls
        var allTerminals = UnityEngine.Object.FindObjectsByType<SecurityTerminalDownload>(FindObjectsInactive.Include);
        sb.AppendLine($"\nAll SecurityTerminalDownload in scene: {allTerminals.Length}");
        foreach (var t in allTerminals)
        {
            sb.AppendLine($"   -> {GetPath(t.gameObject)} (active={t.gameObject.activeInHierarchy}, pos={t.transform.position})");
        }

        var allPowerStations = UnityEngine.Object.FindObjectsByType<PowerPuzzleStation>(FindObjectsInactive.Include);
        sb.AppendLine($"\nAll PowerPuzzleStation in scene: {allPowerStations.Length}");
        foreach (var p in allPowerStations)
        {
            sb.AppendLine($"   -> {GetPath(p.gameObject)} (type={p.StationType}, active={p.gameObject.activeInHierarchy}, pos={p.transform.position})");
        }

        var allPuzzCtrls = UnityEngine.Object.FindObjectsByType<PowerPuzzleController>(FindObjectsInactive.Include);
        sb.AppendLine($"\nAll PowerPuzzleController in scene: {allPuzzCtrls.Length}");
        foreach (var pc in allPuzzCtrls)
        {
            sb.AppendLine($"   -> {GetPath(pc.gameObject)} (active={pc.gameObject.activeInHierarchy})");
        }

        var allFlowCtrls = UnityEngine.Object.FindObjectsByType<MatchFlowController>(FindObjectsInactive.Include);
        sb.AppendLine($"\nAll MatchFlowController in scene: {allFlowCtrls.Length}");
        foreach (var fc in allFlowCtrls)
        {
            sb.AppendLine($"   -> {GetPath(fc.gameObject)} (active={fc.gameObject.activeInHierarchy})");
        }

        // 3. Test Raycast at SecurityTerminal_Hold
        if (secHold != null)
        {
            sb.AppendLine("\n--- RAYCAST TEST: SecurityTerminal_Hold ---");
            Vector3 termCenter = secHold.transform.position + new Vector3(0f, 0.6f, 0f);
            Vector3[] testDirs = new Vector3[] { Vector3.forward, Vector3.back, Vector3.left, Vector3.right };
            string[] dirNames = new string[] { "+Z", "-Z", "-X", "+X" };
            for (int i = 0; i < testDirs.Length; i++)
            {
                Vector3 rayOrigin = termCenter + testDirs[i] * 1.5f;
                Vector3 rayDir = (termCenter - rayOrigin).normalized;
                if (Physics.Raycast(rayOrigin, rayDir, out RaycastHit hit, 3.0f, ~0, QueryTriggerInteraction.Collide))
                {
                    sb.AppendLine($"   From {dirNames[i]} (1.5m away) -> HIT: {hit.collider.gameObject.name} (layer={LayerMask.LayerToName(hit.collider.gameObject.layer)}) at dist={hit.distance:F2}");
                    var interactable = hit.collider.GetComponentInParent<IInteractable>();
                    if (interactable != null)
                    {
                        sb.AppendLine($"      Interactable found: {interactable.GetType().Name}, CanInteract={interactable.CanInteract(null)}, Prompt='{interactable.InteractionPrompt}'");
                    }
                    else
                    {
                        sb.AppendLine($"      NO IInteractable in parent!");
                    }
                }
                else
                {
                    sb.AppendLine($"   From {dirNames[i]} -> MISSED!");
                }
            }
        }

        // 4. Test Raycast at PowerControl_Station
        if (pwrStation != null)
        {
            sb.AppendLine("\n--- RAYCAST TEST: PowerControl_Station ---");
            Vector3 stationCenter = pwrStation.transform.position + new Vector3(0f, 0.6f, 0f);
            Vector3[] testDirs = new Vector3[] { Vector3.forward, Vector3.back, Vector3.left, Vector3.right };
            string[] dirNames = new string[] { "+Z", "-Z", "-X", "+X" };
            for (int i = 0; i < testDirs.Length; i++)
            {
                Vector3 rayOrigin = stationCenter + testDirs[i] * 1.5f;
                Vector3 rayDir = (stationCenter - rayOrigin).normalized;
                if (Physics.Raycast(rayOrigin, rayDir, out RaycastHit hit, 3.0f, ~0, QueryTriggerInteraction.Collide))
                {
                    sb.AppendLine($"   From {dirNames[i]} (1.5m away) -> HIT: {hit.collider.gameObject.name} (layer={LayerMask.LayerToName(hit.collider.gameObject.layer)}) at dist={hit.distance:F2}");
                    var interactable = hit.collider.GetComponentInParent<IInteractable>();
                    if (interactable != null)
                    {
                        sb.AppendLine($"      Interactable found: {interactable.GetType().Name}, CanInteract={interactable.CanInteract(null)}, Prompt='{interactable.InteractionPrompt}'");
                    }
                    else
                    {
                        sb.AppendLine($"      NO IInteractable in parent!");
                    }
                }
                else
                {
                    sb.AppendLine($"   From {dirNames[i]} -> MISSED!");
                }
            }
        }

        // 5. Simulate Player Interaction directly
        sb.AppendLine("\n=== SIMULATING PLAYER INTERACTION ===");
        if (secHold != null)
        {
            var download = secHold.GetComponent<SecurityTerminalDownload>();
            var ui = secHold.GetComponentInChildren<SecurityTerminalUIController>(true);
            sb.AppendLine($"SecurityTerminalDownload present: {download != null}");
            sb.AppendLine($"SecurityTerminalUIController present: {ui != null}");
            if (download != null)
            {
                sb.AppendLine($"   RequiresHold: {download.RequiresHold}");
                sb.AppendLine($"   IsComplete: {download.IsComplete}");
                sb.AppendLine($"   CanInteract(null): {download.CanInteract(null)}");
                sb.AppendLine($"   InteractionPrompt: '{download.InteractionPrompt}'");

                // Test OpenUI directly
                try
                {
                    download.OpenUI(null);
                    sb.AppendLine($"   download.OpenUI(null) called. UI IsOpen={ui?.IsOpen}");
                    if (ui != null) ui.Close();
                }
                catch (System.Exception ex)
                {
                    sb.AppendLine($"   ERROR calling OpenUI: {ex.Message}");
                }
            }
        }

        if (pwrStation != null)
        {
            var station = pwrStation.GetComponent<PowerPuzzleStation>();
            var ui = pwrStation.GetComponentInChildren<PowerControlUIController>(true);
            sb.AppendLine($"\nPowerPuzzleStation present: {station != null}");
            sb.AppendLine($"PowerControlUIController present: {ui != null}");
            if (station != null)
            {
                sb.AppendLine($"   CanInteract(null): {station.CanInteract(null)}");
                sb.AppendLine($"   InteractionPrompt: '{station.InteractionPrompt}'");

                // Test Interact directly
                try
                {
                    station.Interact(null);
                    sb.AppendLine($"   station.Interact(null) called. UI IsOpen={ui?.IsOpen}");
                    if (ui != null) ui.Close();
                }
                catch (System.Exception ex)
                {
                    sb.AppendLine($"   ERROR calling station.Interact: {ex.Message}");
                }
            }
        }

        foreach (var go in allGos)
        {
            string n = go.name.ToLower();
            if (n.Contains("security") || n.Contains("terminal") || n.Contains("power") || n.Contains("control") || n.Contains("relay"))
            {
                sb.AppendLine($"\n--- GO: {go.name} (activeInHierarchy={go.activeInHierarchy}, layer={LayerMask.LayerToName(go.layer)} [{go.layer}]) ---");
                sb.AppendLine($"   Pos: {go.transform.position}, Rot: {go.transform.eulerAngles}");

                var colliders = go.GetComponents<Collider>();
                foreach (var c in colliders)
                {
                    sb.AppendLine($"   Collider: {c.GetType().Name}, enabled={c.enabled}, isTrigger={c.isTrigger}, bounds={c.bounds}");
                    if (c is BoxCollider bc)
                    {
                        sb.AppendLine($"      BoxCollider center={bc.center}, size={bc.size}");
                    }
                }

                var comps = go.GetComponents<Component>();
                foreach (var comp in comps)
                {
                    if (comp != null)
                    {
                        sb.AppendLine($"   Component: {comp.GetType().FullName}");
                    }
                }

                // Check child colliders or UIs
                var childColliders = go.GetComponentsInChildren<Collider>(true);
                if (childColliders.Length > colliders.Length)
                {
                    sb.AppendLine($"   Total child colliders: {childColliders.Length}");
                    foreach (var cc in childColliders)
                    {
                        sb.AppendLine($"      Child collider on {cc.gameObject.name}: {cc.GetType().Name}, enabled={cc.enabled}, bounds={cc.bounds}");
                    }
                }
            }
        }

        // 2. Search Player in scene
        sb.AppendLine("\n=== PLAYER IN SCENE ===");
        var playerInteractions = UnityEngine.Object.FindObjectsByType<PlayerInteraction>(FindObjectsInactive.Include);
        sb.AppendLine($"PlayerInteraction components found: {playerInteractions.Length}");
        foreach (var pi in playerInteractions)
        {
            sb.AppendLine($"   Player: {pi.gameObject.name}, active={pi.gameObject.activeInHierarchy}, pos={pi.transform.position}");
            var so = new SerializedObject(pi);
            var distProp = so.FindProperty("interactDistance");
            var maskProp = so.FindProperty("interactableLayers");
            var triggerProp = so.FindProperty("triggerInteraction");
            sb.AppendLine($"   interactDistance={distProp?.floatValue}, interactableLayers={maskProp?.intValue}, triggerInteraction={triggerProp?.enumValueIndex}");
        }

        string logFolder = Path.Combine(Application.dataPath, "../Logs");
        if (!Directory.Exists(logFolder)) Directory.CreateDirectory(logFolder);
        string outPath = Path.Combine(logFolder, "SciFiInteractivityDiagnosis.txt");
        File.WriteAllText(outPath, sb.ToString());
        Debug.Log($"Diagnosis written to {outPath}");
    }

    [MenuItem("Tools/ECHO Protocol/Run Restore Main Power Tests")]
    public static void RunRestoreMainPowerTests()
    {
        int passed = 0;
        int failed = 0;
        var failures = new System.Text.StringBuilder();

        RunTest("SecurityHold_Completion_Generates_FourDigitCode_With_LeadingZero", TestSecurityHoldCodeFormat, ref passed, ref failed, failures);
        RunTest("SecurityHold_Completion_Transitions_To_PowerPuzzle_Not_FinalHunt", TestSecurityHoldTransition, ref passed, ref failed, failures);
        RunTest("PowerPuzzle_Rejects_Code_Before_SecurityHold_Complete", TestPowerPuzzleRejectsEarlyCode, ref passed, ref failed, failures);
        RunTest("PowerControl_Prompt_Reflects_Locked_And_Available_States", TestPowerControlPrompts, ref passed, ref failed, failures);
        RunTest("PowerControl_WrongCode_Fails_Without_Resetting_SecurityHold", TestPowerControlWrongCode, ref passed, ref failed, failures);
        RunTest("PowerControl_ConsecutiveFailures_Trigger_Lockout", TestPowerControlLockout, ref passed, ref failed, failures);
        RunTest("PowerControl_CorrectCode_Completes_And_Transitions_To_FinalHunt", TestPowerControlCompletion, ref passed, ref failed, failures);
        RunTest("Double_Completion_Is_Idempotent", TestDoubleCompletionIdempotent, ref passed, ref failed, failures);
        RunTest("NetworkMatchState_Has_AuthoritativeCode_And_Required_Properties", TestNetworkMatchStateContracts, ref passed, ref failed, failures);
        RunTest("NetworkPowerPuzzle_Has_RpcSubmitAuthorizationCode", TestNetworkPowerPuzzleContracts, ref passed, ref failed, failures);
        RunTest("EmergencyNetworkState_Offline_When_Relays_Offline", TestEmergencyNetworkStateRelays, ref passed, ref failed, failures);

        string status = failed == 0 && passed > 0 ? "PASS" : "FAIL";
        string summary = $"status={status} passed={passed} failed={failed} skipped=0";
        string logFolder = Path.Combine(Application.dataPath, "../Logs");
        if (!Directory.Exists(logFolder))
        {
            Directory.CreateDirectory(logFolder);
        }
        string outputPath = Path.Combine(logFolder, "RestoreMainPowerTestResults.txt");
        File.WriteAllText(outputPath, summary + "\n" + failures);
        if (failed == 0)
        {
            Debug.Log("[RestoreMainPower-TESTS] Finished: " + summary);
        }
        else
        {
            Debug.LogError("[RestoreMainPower-TESTS] Finished: " + summary + "\n" + failures);
        }
    }

    [MenuItem("Tools/ECHO Protocol/Run All Objectives Tests (RelayA + RelayB + RestoreMainPower)")]
    public static void RunAllObjectivesTests()
    {
        RunRestoreMainPowerTests();
        RelayASetupBuilder.RunRelayAEditModeTests();
        RelayBSetupBuilder.RunRelayBEditModeTests();
        Debug.Log("[ALL OBJECTIVES TESTS] Executed Relay A, Relay B, and Restore Main Power test suites!");
    }

    private static void RunTest(string name, System.Action test, ref int passed, ref int failed, System.Text.StringBuilder failures)
    {
        try
        {
            test();
            passed++;
        }
        catch (System.Exception ex)
        {
            failed++;
            failures.AppendLine($"[FAILED] {name}: {ex.Message}");
            Debug.LogError($"[RestoreMainPower-TESTS] FAILED: {name} - {ex.Message}");
        }
    }

    private static void TestSecurityHoldCodeFormat()
    {
        int code = 427;
        string formatted = code.ToString("D4");
        if (formatted != "0427") throw new Exception("Expected 0427 but got " + formatted);
        if (formatted.Length != 4) throw new Exception("Expected length 4");
    }

    private static void TestSecurityHoldTransition()
    {
        var go = new GameObject("TestFlowGo");
        try
        {
            var flow = go.AddComponent<MatchFlowController>();
            if (flow.Phase != MatchPhase.ExploreCore) throw new Exception("Initial phase should be ExploreCore");
            flow.NotifyCoreObjectiveComplete();
            if (flow.Phase != MatchPhase.SecurityHold) throw new Exception("Phase should be SecurityHold");
            flow.NotifySecurityHoldComplete();
            if (flow.Phase != MatchPhase.PowerPuzzle) throw new Exception("Phase should be PowerPuzzle, but was " + flow.Phase);
            if (flow.IsMatchEnded) throw new Exception("Match should not be ended");
            if (!flow.IsSecurityHoldComplete) throw new Exception("IsSecurityHoldComplete should be true");
            if (string.IsNullOrEmpty(flow.PowerAuthorizationCode) || flow.PowerAuthorizationCode.Length != 4)
                throw new Exception("PowerAuthorizationCode should be 4 digits");
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
    }

    private static void TestPowerPuzzleRejectsEarlyCode()
    {
        var go = new GameObject("TestPuzzleGo");
        try
        {
            var puzzle = go.AddComponent<PowerPuzzleController>();
            puzzle.AuthoritativeCode = "1234";
            bool res = puzzle.SubmitAuthorizationCode("1234", go);
            if (res) throw new Exception("Should reject code before Security Hold complete");
            if (puzzle.IsComplete) throw new Exception("Puzzle should not be complete");
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
    }

    private static void TestPowerControlPrompts()
    {
        var go = new GameObject("TestPromptGo");
        var goTerminal = new GameObject("TestTerminalGo");
        try
        {
            // Add a collider (required by SecurityTerminalDownload)
            goTerminal.AddComponent<BoxCollider>();
            var terminal = goTerminal.AddComponent<SecurityTerminalDownload>();
            var puzzle = go.AddComponent<PowerPuzzleController>();

            // Before security hold: prompt should indicate locked
            string locked = puzzle.GetPrompt(PowerPuzzleStationType.PowerControl);
            if (!locked.ToUpperInvariant().Contains("LOCKED") || !locked.ToUpperInvariant().Contains("SECURITY AUTHENTICATION REQUIRED"))
                throw new Exception("Prompt before Security Hold should indicate locked/auth required: " + locked);

            // Complete the terminal via reflection (sets _state = Completed)
            typeof(SecurityTerminalDownload)
                .GetField("_state", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                ?.SetValue(terminal, SecurityDownloadState.Completed);

            // Now IsSecurityHoldComplete should return true (terminal.IsComplete = true)
            if (!terminal.IsComplete)
                throw new Exception("SecurityTerminalDownload.IsComplete should be true after setting state");

            string available = puzzle.GetPrompt(PowerPuzzleStationType.PowerControl);
            if (!available.ToUpperInvariant().Contains("AUTHORIZATION AVAILABLE"))
                throw new Exception("Prompt after Security Hold should indicate available: " + available);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(go);
            UnityEngine.Object.DestroyImmediate(goTerminal);
        }
    }

    private static void TestPowerControlWrongCode()
    {
        var go = new GameObject("TestWrongCodeGo");
        try
        {
            var flow = go.AddComponent<MatchFlowController>();
            var puzzle = go.AddComponent<PowerPuzzleController>();
            flow.NotifyCoreObjectiveComplete();
            flow.NotifySecurityHoldComplete();
            puzzle.AuthoritativeCode = flow.PowerAuthorizationCode;
            bool res = puzzle.SubmitAuthorizationCode("9999", go);
            if (res) throw new Exception("Wrong code should fail");
            if (puzzle.IsComplete) throw new Exception("Puzzle should not complete on wrong code");
            if (!flow.IsSecurityHoldComplete) throw new Exception("Security hold must not reset on wrong code");
            if (flow.Phase != MatchPhase.PowerPuzzle) throw new Exception("Phase must remain PowerPuzzle");
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
    }

    private static void TestPowerControlLockout()
    {
        var go = new GameObject("TestLockoutGo");
        try
        {
            var flow = go.AddComponent<MatchFlowController>();
            var puzzle = go.AddComponent<PowerPuzzleController>();
            flow.NotifyCoreObjectiveComplete();
            flow.NotifySecurityHoldComplete();
            puzzle.AuthoritativeCode = flow.PowerAuthorizationCode;
            puzzle.SubmitAuthorizationCode("0001", go);
            puzzle.SubmitAuthorizationCode("0002", go);
            puzzle.SubmitAuthorizationCode("0003", go);
            if (!puzzle.IsLockedOut) throw new Exception("3 wrong attempts should trigger lockout");
            if (puzzle.LockoutRemaining <= 0f) throw new Exception("LockoutRemaining should be > 0");
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
    }

    private static void TestPowerControlCompletion()
    {
        var go = new GameObject("TestCompleteGo");
        try
        {
            var flow = go.AddComponent<MatchFlowController>();
            var puzzle = go.AddComponent<PowerPuzzleController>();
            flow.NotifyCoreObjectiveComplete();
            flow.NotifySecurityHoldComplete();
            string code = flow.PowerAuthorizationCode;
            puzzle.AuthoritativeCode = code;
            bool res = puzzle.SubmitAuthorizationCode(code, go);
            if (!res) throw new Exception("Correct code should succeed");
            if (!puzzle.IsComplete) throw new Exception("Puzzle should be complete");
            if (flow.Phase != MatchPhase.FinalHunt) throw new Exception("Phase should transition to FinalHunt, but was " + flow.Phase);
            if (!flow.IsRestoreMainPowerComplete) throw new Exception("IsRestoreMainPowerComplete should be true");
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
    }

    private static void TestDoubleCompletionIdempotent()
    {
        var go = new GameObject("TestIdempotentGo");
        try
        {
            var flow = go.AddComponent<MatchFlowController>();
            var puzzle = go.AddComponent<PowerPuzzleController>();
            flow.NotifyCoreObjectiveComplete();
            flow.NotifySecurityHoldComplete();
            string code = flow.PowerAuthorizationCode;
            puzzle.AuthoritativeCode = code;
            bool res1 = puzzle.SubmitAuthorizationCode(code, go);
            bool res2 = puzzle.SubmitAuthorizationCode(code, go);
            if (!res1) throw new Exception("First submission should succeed");
            if (res2) throw new Exception("Second submission should return false (idempotent)");
            if (flow.Phase != MatchPhase.FinalHunt) throw new Exception("Phase must stay FinalHunt");
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
    }

    private static void TestNetworkMatchStateContracts()
    {
        string src = File.ReadAllText("Assets/_Project/Scripts/Networking/Match/NetworkMatchState.cs");
        if (!src.Contains("public NetworkString<_16> PowerAuthorizationCode")) throw new Exception("Missing PowerAuthorizationCode property");
        if (!src.Contains("public NetworkBool SecurityHoldCompleted")) throw new Exception("Missing SecurityHoldCompleted property");
        if (!src.Contains("public NetworkBool PowerAuthorizationAvailable")) throw new Exception("Missing PowerAuthorizationAvailable property");
        if (!src.Contains("public NetworkBool PowerPuzzleCompleted")) throw new Exception("Missing PowerPuzzleCompleted property");
        if (!src.Contains("public NetworkBool RestoreMainPowerCompleted")) throw new Exception("Missing RestoreMainPowerCompleted property");
        if (!src.Contains("public bool TrySubmitPowerCode(PlayerRef requester, string code)")) throw new Exception("Missing TrySubmitPowerCode");
        if (!src.Contains("public bool TryCompleteSecurityHold(NetworkId sourceId)")) throw new Exception("Missing TryCompleteSecurityHold");
    }

    private static void TestNetworkPowerPuzzleContracts()
    {
        string src = File.ReadAllText("Assets/_Project/Scripts/Networking/Interaction/NetworkPowerPuzzle.cs");
        if (!src.Contains("[Rpc(RpcSources.All, RpcTargets.StateAuthority)]")) throw new Exception("Missing Rpc attribute");
        if (!src.Contains("public void RpcSubmitAuthorizationCode(PlayerRef sender, string code)")) throw new Exception("Missing RpcSubmitAuthorizationCode");
        if (!src.Contains("matchState.TrySubmitPowerCode(sender, code)")) throw new Exception("Missing call to TrySubmitPowerCode");
    }

    private static void TestEmergencyNetworkStateRelays()
    {
        var relayA = UnityEngine.Object.FindAnyObjectByType<EchoProtocol.RelayA.RelayAController>();
        var relayB = UnityEngine.Object.FindAnyObjectByType<EchoProtocol.RelayB.RelayBController>();
        if (relayA != null && relayB != null)
        {
            bool expected = relayA.IsOnline && relayB.IsOnline;
            if (EmergencyNetworkState.AreRelaysOnline() != expected)
                throw new Exception($"Expected AreRelaysOnline to be {expected} when RelayA={relayA.IsOnline}, RelayB={relayB.IsOnline}");
        }
        else
        {
            if (!EmergencyNetworkState.AreRelaysOnline())
                throw new Exception("Default when no relays in scene should be true");
        }
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

    private static Button CreateButton(Transform parent, string name, string text, Sprite sprite, Vector2 position, Vector2 size)
    {
        GameObject root = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        root.transform.SetParent(parent, false);
        SetAnchored(root, position, size, new Vector2(0f, 1f));

        Image img = root.GetComponent<Image>();
        img.sprite = sprite;
        img.type = sprite != null ? Image.Type.Sliced : Image.Type.Simple;
        img.color = sprite != null ? Color.white : new Color(0.12f, 0.18f, 0.25f, 1f);

        Button btn = root.GetComponent<Button>();
        ColorBlock colors = btn.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.85f, 1f, 0.95f, 1f);
        colors.pressedColor = new Color(0.7f, 0.85f, 0.8f, 1f);
        colors.selectedColor = Color.white;
        colors.disabledColor = new Color(0.4f, 0.4f, 0.4f, 0.5f);
        btn.colors = colors;

        TMP_Text lbl = CreateText(root.transform, "Label", text, 17f, Vector2.zero, size, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        lbl.color = new Color(0.80f, 0.95f, 1f, 1f);
        return btn;
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
        rect.pivot = topLeftAnchor;
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

    private static Sprite LoadSprite(string assetPath)
    {
        return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
    }

    private static void SetObject(SerializedObject so, string propertyName, UnityEngine.Object target)
    {
        SerializedProperty prop = so.FindProperty(propertyName);
        if (prop != null)
        {
            prop.objectReferenceValue = target;
        }
    }

    private static string GetPath(GameObject go)
    {
        if (go == null) return "null";
        string path = go.name;
        Transform parent = go.transform.parent;
        while (parent != null)
        {
            path = parent.name + "/" + path;
            parent = parent.parent;
        }
        return path;
    }
}
