using System.IO;
using EchoProtocol.UI.HUD;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace EchoProtocol.EditorTools
{
    public static class GameplayHUDSceneSetup
    {
        private const string RunMarker = "Assets/Editor/.run_setup_gameplay_hud";
        private const string ScenePath = "Assets/Scenes/SciFi.unity";
        private static Font _defaultFont;

        [InitializeOnLoadMethod]
        private static void CheckRunMarker()
        {
            EditorApplication.delayCall += () =>
            {
                string fullMarkerPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", RunMarker));
                if (File.Exists(fullMarkerPath))
                {
                    try
                    {
                        File.Delete(fullMarkerPath);
                    }
                    catch
                    {
                        // Ignore delete errors
                    }

                    SetupCompleteGameplayHUD();
                }
            };
        }

        private static Font GetDefaultFont()
        {
            if (_defaultFont != null) return _defaultFont;
            _defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (_defaultFont == null)
            {
                _defaultFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }
            return _defaultFont;
        }

        [MenuItem("Tools/ECHO Protocol/Setup Complete Gameplay HUD")]
        public static void SetupCompleteGameplayHUD()
        {
            if (!EditorApplication.isPlaying)
            {
                var activeScene = EditorSceneManager.GetActiveScene();
                if (activeScene.path != ScenePath && File.Exists(ScenePath))
                {
                    EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                }
            }

            EnsureEventSystem();

            GameObject canvasGo = GameObject.Find("GameplayHUD_Canvas");
            if (canvasGo != null)
            {
                Undo.DestroyObjectImmediate(canvasGo);
            }

            canvasGo = new GameObject("GameplayHUD_Canvas");
            Undo.RegisterCreatedObjectUndo(canvasGo, "Create GameplayHUD_Canvas");

            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            scaler.dynamicPixelsPerUnit = 2.0f;

            canvasGo.AddComponent<GraphicRaycaster>();
            GameplayHUDManager manager = canvasGo.AddComponent<GameplayHUDManager>();

            // 1. Interaction Prompt (Center-Low, NO Crosshair/Reticle)
            HUDInteractionPrompt prompt = CreateInteractionPrompt(canvasGo.transform);

            // 2. Objective Tracker (Top-Center)
            HUDObjectiveTracker objective = CreateObjectiveTracker(canvasGo.transform);

            // 3. Player Vitals (Bottom-Left)
            HUDPlayerVitals vitals = CreatePlayerVitals(canvasGo.transform);

            // 4. Hotbar (Bottom-Right)
            HUDHotbar hotbar = CreateHotbar(canvasGo.transform);

            // 5. Teammate Status (Top-Left)
            HUDTeammateStatus teammates = CreateTeammateStatus(canvasGo.transform);

            // 6. 3D World Markers (Full Screen Overlay)
            HUD3DWorldMarker markers = Create3DWorldMarkers(canvasGo.transform);

            // Wire up manager
            var managerSo = new SerializedObject(manager);
            managerSo.FindProperty("interactionPrompt").objectReferenceValue = prompt;
            managerSo.FindProperty("objectiveTracker").objectReferenceValue = objective;
            managerSo.FindProperty("playerVitals").objectReferenceValue = vitals;
            managerSo.FindProperty("hotbar").objectReferenceValue = hotbar;
            managerSo.FindProperty("teammateStatus").objectReferenceValue = teammates;
            managerSo.FindProperty("worldMarker").objectReferenceValue = markers;
            managerSo.ApplyModifiedProperties();

            // Save Prefab
            string prefabDir = "Assets/Prefabs/UI";
            if (!Directory.Exists(prefabDir))
            {
                Directory.CreateDirectory(prefabDir);
            }
            string prefabPath = $"{prefabDir}/PF_GameplayHUD_Canvas.prefab";
            PrefabUtility.SaveAsPrefabAssetAndConnect(canvasGo, prefabPath, InteractionMode.AutomatedAction);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log($"<color=#00E5FF>[ECHO Protocol]</color> Complete Gameplay HUD setup successful! Saved to {prefabPath}");
        }

        private static void EnsureEventSystem()
        {
            if (Object.FindAnyObjectByType<EventSystem>() == null)
            {
                GameObject es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<StandaloneInputModule>();
                Undo.RegisterCreatedObjectUndo(es, "Create EventSystem");
            }
        }

        private static HUDInteractionPrompt CreateInteractionPrompt(Transform parent)
        {
            GameObject promptGo = CreateUIObject("InteractionPrompt_Panel", parent);
            RectTransform rt = promptGo.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0, -140); // Slightly below center
            rt.sizeDelta = new Vector2(460, 76);

            CanvasGroup cg = promptGo.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            cg.interactable = false;
            cg.blocksRaycasts = false;

            // Panel Background
            Image bg = promptGo.AddComponent<Image>();
            bg.sprite = HUDTextureUtility.RoundedBox;
            bg.type = Image.Type.Sliced;
            bg.color = new Color(0.02f, 0.04f, 0.08f, 0.96f);

            Outline outline = promptGo.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0.85f, 1f, 0.5f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            // Key badge container
            GameObject badgeGo = CreateUIObject("KeyBadge", promptGo.transform);
            RectTransform badgeRt = badgeGo.GetComponent<RectTransform>();
            badgeRt.anchorMin = new Vector2(0f, 0.5f);
            badgeRt.anchorMax = new Vector2(0f, 0.5f);
            badgeRt.pivot = new Vector2(0f, 0.5f);
            badgeRt.anchoredPosition = new Vector2(16, 0);
            badgeRt.sizeDelta = new Vector2(42, 42);
            Image badgeImg = badgeGo.AddComponent<Image>();
            badgeImg.sprite = HUDTextureUtility.RoundedBox;
            badgeImg.type = Image.Type.Sliced;
            badgeImg.color = new Color(0f, 0.9f, 1f, 0.35f);

            Outline badgeOutline = badgeGo.AddComponent<Outline>();
            badgeOutline.effectColor = new Color(0f, 0.9f, 1f, 0.8f);
            badgeOutline.effectDistance = new Vector2(1f, -1f);

            Text keyText = CreateText("KeyText", badgeGo.transform, "E", 20, FontStyle.Bold, Color.white);
            RectTransform keyTextRt = keyText.GetComponent<RectTransform>();
            keyTextRt.anchorMin = Vector2.zero;
            keyTextRt.anchorMax = Vector2.one;
            keyTextRt.sizeDelta = Vector2.zero;
            keyText.alignment = TextAnchor.MiddleCenter;

            // Prompt Text
            Text promptText = CreateText("PromptLabel", promptGo.transform, "Tương tác [E]", 18, FontStyle.Bold, Color.white);
            RectTransform ptRt = promptText.GetComponent<RectTransform>();
            ptRt.anchorMin = new Vector2(0f, 0.5f);
            ptRt.anchorMax = new Vector2(1f, 0.5f);
            ptRt.pivot = new Vector2(0f, 0.5f);
            ptRt.anchoredPosition = new Vector2(70, 0);
            ptRt.sizeDelta = new Vector2(-160, 44);
            promptText.alignment = TextAnchor.MiddleLeft;

            // Hold Ring Container
            GameObject ringContainer = CreateUIObject("HoldRingContainer", promptGo.transform);
            RectTransform ringRt = ringContainer.GetComponent<RectTransform>();
            ringRt.anchorMin = new Vector2(1f, 0.5f);
            ringRt.anchorMax = new Vector2(1f, 0.5f);
            ringRt.pivot = new Vector2(1f, 0.5f);
            ringRt.anchoredPosition = new Vector2(-16, 0);
            ringRt.sizeDelta = new Vector2(48, 48);

            // Ring BG
            GameObject ringBgGo = CreateUIObject("RingBG", ringContainer.transform);
            RectTransform ringBgRt = ringBgGo.GetComponent<RectTransform>();
            ringBgRt.anchorMin = Vector2.zero;
            ringBgRt.anchorMax = Vector2.one;
            ringBgRt.sizeDelta = Vector2.zero;
            Image ringBg = ringBgGo.AddComponent<Image>();
            ringBg.sprite = HUDTextureUtility.CircleRing;
            ringBg.color = new Color(0.2f, 0.3f, 0.4f, 0.5f);

            // Ring Fill
            GameObject ringFillGo = CreateUIObject("RingFill", ringContainer.transform);
            RectTransform ringFillRt = ringFillGo.GetComponent<RectTransform>();
            ringFillRt.anchorMin = Vector2.zero;
            ringFillRt.anchorMax = Vector2.one;
            ringFillRt.sizeDelta = Vector2.zero;
            Image ringFill = ringFillGo.AddComponent<Image>();
            ringFill.sprite = HUDTextureUtility.CircleRing;
            ringFill.type = Image.Type.Filled;
            ringFill.fillMethod = Image.FillMethod.Radial360;
            ringFill.fillOrigin = (int)Image.Origin360.Top;
            ringFill.fillClockwise = true;
            ringFill.fillAmount = 0f;
            ringFill.color = new Color(0f, 0.9f, 1f, 1f);

            // Percent Text
            Text percentText = CreateText("PercentText", ringContainer.transform, "0%", 13, FontStyle.Bold, Color.white);
            RectTransform pctRt = percentText.GetComponent<RectTransform>();
            pctRt.anchorMin = Vector2.zero;
            pctRt.anchorMax = Vector2.one;
            pctRt.sizeDelta = Vector2.zero;
            percentText.alignment = TextAnchor.MiddleCenter;

            ringContainer.SetActive(false);

            HUDInteractionPrompt comp = promptGo.AddComponent<HUDInteractionPrompt>();
            var so = new SerializedObject(comp);
            so.FindProperty("promptCanvasGroup").objectReferenceValue = cg;
            so.FindProperty("promptText").objectReferenceValue = promptText;
            so.FindProperty("holdProgressContainer").objectReferenceValue = ringContainer;
            so.FindProperty("holdProgressRing").objectReferenceValue = ringFill;
            so.FindProperty("holdProgressText").objectReferenceValue = percentText;
            so.ApplyModifiedProperties();

            return comp;
        }

        private static HUDObjectiveTracker CreateObjectiveTracker(Transform parent)
        {
            GameObject objGo = CreateUIObject("ObjectiveTracker_Panel", parent);
            RectTransform rt = objGo.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0, -20);
            rt.sizeDelta = new Vector2(740, 116);

            Image bg = objGo.AddComponent<Image>();
            bg.sprite = HUDTextureUtility.RoundedBox;
            bg.type = Image.Type.Sliced;
            bg.color = new Color(0.02f, 0.04f, 0.08f, 0.96f);

            Outline outline = objGo.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0.85f, 1f, 0.45f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            // Top Header Glow
            GameObject glowGo = CreateUIObject("HeaderGlow", objGo.transform);
            RectTransform glowRt = glowGo.GetComponent<RectTransform>();
            glowRt.anchorMin = new Vector2(0f, 1f);
            glowRt.anchorMax = new Vector2(1f, 1f);
            glowRt.pivot = new Vector2(0.5f, 1f);
            glowRt.anchoredPosition = new Vector2(0, 0);
            glowRt.sizeDelta = new Vector2(0, 4);
            Image headerGlow = glowGo.AddComponent<Image>();
            headerGlow.sprite = HUDTextureUtility.WhitePixel;
            headerGlow.color = new Color(0f, 0.9f, 1f, 0.9f);

            // Top Phase Badge
            Text phaseBadge = CreateText("PhaseBadge", objGo.transform, "GIAI ĐOẠN 1 // THU THẬP NĂNG LƯỢNG", 14, FontStyle.Bold, new Color(0f, 0.9f, 1f, 1f));
            RectTransform pbRt = phaseBadge.GetComponent<RectTransform>();
            pbRt.anchorMin = new Vector2(0f, 1f);
            pbRt.anchorMax = new Vector2(1f, 1f);
            pbRt.pivot = new Vector2(0.5f, 1f);
            pbRt.anchoredPosition = new Vector2(0, -10);
            pbRt.sizeDelta = new Vector2(-40, 20);
            phaseBadge.alignment = TextAnchor.MiddleCenter;

            // Objective Title
            Text objTitle = CreateText("ObjectiveTitle", objGo.transform, "TÌM VÀ VẬN CHUYỂN ENERGY CORE", 20, FontStyle.Bold, Color.white);
            RectTransform otRt = objTitle.GetComponent<RectTransform>();
            otRt.anchorMin = new Vector2(0f, 1f);
            otRt.anchorMax = new Vector2(1f, 1f);
            otRt.pivot = new Vector2(0.5f, 1f);
            otRt.anchoredPosition = new Vector2(0, -36);
            otRt.sizeDelta = new Vector2(-40, 26);
            objTitle.alignment = TextAnchor.MiddleCenter;

            // Objective Detail
            Text objDetail = CreateText("ObjectiveDetail", objGo.transform, "Tìm và mang 3 Energy Core về Sector Box [0/3]", 15, FontStyle.Normal, new Color(0.85f, 0.95f, 1f, 1f));
            RectTransform odRt = objDetail.GetComponent<RectTransform>();
            odRt.anchorMin = new Vector2(0f, 1f);
            odRt.anchorMax = new Vector2(1f, 1f);
            odRt.pivot = new Vector2(0.5f, 1f);
            odRt.anchoredPosition = new Vector2(0, -66);
            odRt.sizeDelta = new Vector2(-40, 22);
            objDetail.alignment = TextAnchor.MiddleCenter;

            // Progress Bar BarBG
            GameObject barBgGo = CreateUIObject("ProgressBarBG", objGo.transform);
            RectTransform barBgRt = barBgGo.GetComponent<RectTransform>();
            barBgRt.anchorMin = new Vector2(0.5f, 0f);
            barBgRt.anchorMax = new Vector2(0.5f, 0f);
            barBgRt.pivot = new Vector2(0.5f, 0f);
            barBgRt.anchoredPosition = new Vector2(0, 10);
            barBgRt.sizeDelta = new Vector2(640, 8);
            Image barBg = barBgGo.AddComponent<Image>();
            barBg.sprite = HUDTextureUtility.WhitePixel;
            barBg.color = new Color(0.1f, 0.16f, 0.24f, 0.95f);

            // Progress Bar Fill
            GameObject fillGo = CreateUIObject("ProgressBarFill", barBgGo.transform);
            RectTransform fillRt = fillGo.GetComponent<RectTransform>();
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = Vector2.one;
            fillRt.sizeDelta = Vector2.zero;
            Image barFill = fillGo.AddComponent<Image>();
            barFill.sprite = HUDTextureUtility.WhitePixel;
            barFill.type = Image.Type.Filled;
            barFill.fillMethod = Image.FillMethod.Horizontal;
            barFill.fillOrigin = (int)Image.OriginHorizontal.Left;
            barFill.fillAmount = 0f;
            barFill.color = new Color(0f, 0.9f, 1f, 1f);

            HUDObjectiveTracker comp = objGo.AddComponent<HUDObjectiveTracker>();
            var so = new SerializedObject(comp);
            so.FindProperty("phaseBadgeText").objectReferenceValue = phaseBadge;
            so.FindProperty("objectiveTitleText").objectReferenceValue = objTitle;
            so.FindProperty("objectiveDetailText").objectReferenceValue = objDetail;
            so.FindProperty("progressBarFill").objectReferenceValue = barFill;
            so.FindProperty("headerGlow").objectReferenceValue = headerGlow;
            so.ApplyModifiedProperties();

            return comp;
        }

        private static HUDPlayerVitals CreatePlayerVitals(Transform parent)
        {
            GameObject vitalsGo = CreateUIObject("PlayerVitals_Panel", parent);
            RectTransform rt = vitalsGo.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0f, 0f);
            rt.anchoredPosition = new Vector2(40, 40);
            rt.sizeDelta = new Vector2(420, 140);

            Image bg = vitalsGo.AddComponent<Image>();
            bg.sprite = HUDTextureUtility.RoundedBox;
            bg.type = Image.Type.Sliced;
            bg.color = new Color(0.02f, 0.04f, 0.08f, 0.96f);

            Outline outline = vitalsGo.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0.85f, 1f, 0.45f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            // Left Accent Bar
            GameObject accentGo = CreateUIObject("AccentBar", vitalsGo.transform);
            RectTransform accRt = accentGo.GetComponent<RectTransform>();
            accRt.anchorMin = new Vector2(0f, 0f);
            accRt.anchorMax = new Vector2(0f, 1f);
            accRt.pivot = new Vector2(0f, 0.5f);
            accRt.anchoredPosition = new Vector2(4, 0);
            accRt.sizeDelta = new Vector2(5, -10);
            Image accImg = accentGo.AddComponent<Image>();
            accImg.sprite = HUDTextureUtility.WhitePixel;
            accImg.color = new Color(0f, 0.9f, 1f, 1f);

            // Stamina Title
            Text stTitle = CreateText("StaminaTitle", vitalsGo.transform, "THỂ LỰC (STAMINA)", 15, FontStyle.Bold, new Color(0f, 0.9f, 1f, 1f));
            RectTransform sttRt = stTitle.GetComponent<RectTransform>();
            sttRt.anchorMin = new Vector2(0f, 1f);
            sttRt.anchorMax = new Vector2(0f, 1f);
            sttRt.pivot = new Vector2(0f, 1f);
            sttRt.anchoredPosition = new Vector2(24, -14);
            sttRt.sizeDelta = new Vector2(220, 22);
            stTitle.alignment = TextAnchor.MiddleLeft;

            // Stamina Value Text
            Text stVal = CreateText("StaminaValue", vitalsGo.transform, "100%", 16, FontStyle.Bold, Color.white);
            RectTransform stvRt = stVal.GetComponent<RectTransform>();
            stvRt.anchorMin = new Vector2(1f, 1f);
            stvRt.anchorMax = new Vector2(1f, 1f);
            stvRt.pivot = new Vector2(1f, 1f);
            stvRt.anchoredPosition = new Vector2(-24, -14);
            stvRt.sizeDelta = new Vector2(90, 22);
            stVal.alignment = TextAnchor.MiddleRight;

            // Stamina Bar BG
            GameObject stBgGo = CreateUIObject("StaminaBarBG", vitalsGo.transform);
            RectTransform stBgRt = stBgGo.GetComponent<RectTransform>();
            stBgRt.anchorMin = new Vector2(0f, 1f);
            stBgRt.anchorMax = new Vector2(1f, 1f);
            stBgRt.pivot = new Vector2(0.5f, 1f);
            stBgRt.anchoredPosition = new Vector2(0, -42);
            stBgRt.sizeDelta = new Vector2(-48, 12);
            Image stBg = stBgGo.AddComponent<Image>();
            stBg.sprite = HUDTextureUtility.WhitePixel;
            stBg.color = new Color(0.1f, 0.16f, 0.24f, 0.95f);

            // Stamina Fill
            GameObject stFillGo = CreateUIObject("StaminaFill", stBgGo.transform);
            RectTransform stFillRt = stFillGo.GetComponent<RectTransform>();
            stFillRt.anchorMin = Vector2.zero;
            stFillRt.anchorMax = Vector2.one;
            stFillRt.sizeDelta = Vector2.zero;
            Image stFill = stFillGo.AddComponent<Image>();
            stFill.sprite = HUDTextureUtility.WhitePixel;
            stFill.type = Image.Type.Filled;
            stFill.fillMethod = Image.FillMethod.Horizontal;
            stFill.fillOrigin = (int)Image.OriginHorizontal.Left;
            stFill.fillAmount = 1f;
            stFill.color = new Color(0f, 0.9f, 1f, 1f);

            // Status Badge BG (Bottom-Left)
            GameObject badgeBgGo = CreateUIObject("StatusBadge", vitalsGo.transform);
            RectTransform bRt = badgeBgGo.GetComponent<RectTransform>();
            bRt.anchorMin = new Vector2(0f, 0f);
            bRt.anchorMax = new Vector2(0f, 0f);
            bRt.pivot = new Vector2(0f, 0f);
            bRt.anchoredPosition = new Vector2(24, 18);
            bRt.sizeDelta = new Vector2(160, 36);
            Image badgeBg = badgeBgGo.AddComponent<Image>();
            badgeBg.sprite = HUDTextureUtility.RoundedBox;
            badgeBg.type = Image.Type.Sliced;
            badgeBg.color = new Color(0f, 0.85f, 0.45f, 0.35f);

            Outline badgeOutline = badgeBgGo.AddComponent<Outline>();
            badgeOutline.effectColor = new Color(0f, 0.9f, 0.45f, 0.8f);
            badgeOutline.effectDistance = new Vector2(1f, -1f);

            Text badgeText = CreateText("BadgeText", badgeBgGo.transform, "BÌNH THƯỜNG", 15, FontStyle.Bold, new Color(0f, 1f, 0.5f, 1f));
            RectTransform btRt = badgeText.GetComponent<RectTransform>();
            btRt.anchorMin = Vector2.zero;
            btRt.anchorMax = Vector2.one;
            btRt.sizeDelta = Vector2.zero;
            badgeText.alignment = TextAnchor.MiddleCenter;

            // Bleedout Container (Bottom-Right of vitals)
            GameObject bleedContainer = CreateUIObject("BleedoutContainer", vitalsGo.transform);
            RectTransform bcRt = bleedContainer.GetComponent<RectTransform>();
            bcRt.anchorMin = new Vector2(0f, 0f);
            bcRt.anchorMax = new Vector2(0f, 0f);
            bcRt.pivot = new Vector2(0f, 0f);
            bcRt.anchoredPosition = new Vector2(200, 18);
            bcRt.sizeDelta = new Vector2(150, 36);

            Text bleedText = CreateText("BleedText", bleedContainer.transform, "HẤP HỐI: 45s", 14, FontStyle.Bold, new Color(1f, 0.3f, 0.3f, 1f));
            RectTransform blRt = bleedText.GetComponent<RectTransform>();
            blRt.anchorMin = new Vector2(0f, 0.5f);
            blRt.anchorMax = new Vector2(1f, 1f);
            blRt.sizeDelta = Vector2.zero;
            bleedText.alignment = TextAnchor.MiddleCenter;

            GameObject blBarBgGo = CreateUIObject("BleedBarBG", bleedContainer.transform);
            RectTransform blBarBgRt = blBarBgGo.GetComponent<RectTransform>();
            blBarBgRt.anchorMin = new Vector2(0f, 0f);
            blBarBgRt.anchorMax = new Vector2(1f, 0.4f);
            blBarBgRt.sizeDelta = Vector2.zero;
            Image blBarBg = blBarBgGo.AddComponent<Image>();
            blBarBg.sprite = HUDTextureUtility.WhitePixel;
            blBarBg.color = new Color(0.12f, 0.18f, 0.26f, 0.9f);

            GameObject blFillGo = CreateUIObject("BleedBarFill", blBarBgGo.transform);
            RectTransform blFillRt = blFillGo.GetComponent<RectTransform>();
            blFillRt.anchorMin = Vector2.zero;
            blFillRt.anchorMax = Vector2.one;
            blFillRt.sizeDelta = Vector2.zero;
            Image blFill = blFillGo.AddComponent<Image>();
            blFill.sprite = HUDTextureUtility.WhitePixel;
            blFill.type = Image.Type.Filled;
            blFill.fillMethod = Image.FillMethod.Horizontal;
            blFill.fillOrigin = (int)Image.OriginHorizontal.Left;
            blFill.fillAmount = 1f;
            blFill.color = new Color(1f, 0.25f, 0.25f, 1f);

            // Noise Container (Floating above vitals panel)
            GameObject noiseGo = CreateUIObject("NoiseContainer", vitalsGo.transform);
            RectTransform nRt = noiseGo.GetComponent<RectTransform>();
            nRt.anchorMin = new Vector2(0f, 1f);
            nRt.anchorMax = new Vector2(1f, 1f);
            nRt.pivot = new Vector2(0f, 0f);
            nRt.anchoredPosition = new Vector2(0, 10);
            nRt.sizeDelta = new Vector2(0, 36);

            CanvasGroup noiseCg = noiseGo.AddComponent<CanvasGroup>();
            noiseCg.alpha = 0f;

            Image noiseBg = noiseGo.AddComponent<Image>();
            noiseBg.sprite = HUDTextureUtility.RoundedBox;
            noiseBg.type = Image.Type.Sliced;
            noiseBg.color = new Color(0.02f, 0.04f, 0.08f, 0.96f);

            Outline noiseOutline = noiseGo.AddComponent<Outline>();
            noiseOutline.effectColor = new Color(1f, 0.7f, 0.1f, 0.6f);
            noiseOutline.effectDistance = new Vector2(1f, -1f);

            GameObject nIconGo = CreateUIObject("NoiseIcon", noiseGo.transform);
            RectTransform niRt = nIconGo.GetComponent<RectTransform>();
            niRt.anchorMin = new Vector2(0f, 0.5f);
            niRt.anchorMax = new Vector2(0f, 0.5f);
            niRt.pivot = new Vector2(0f, 0.5f);
            niRt.anchoredPosition = new Vector2(12, 0);
            niRt.sizeDelta = new Vector2(30, 24);
            Image noiseIcon = nIconGo.AddComponent<Image>();
            noiseIcon.sprite = HUDTextureUtility.SoundWave;
            noiseIcon.color = new Color(1f, 0.75f, 0.1f, 1f);

            Text noiseLabel = CreateText("NoiseLabel", noiseGo.transform, "TIẾNG ĐỘNG // CHẠY NHANH", 14, FontStyle.Bold, new Color(1f, 0.8f, 0.2f, 1f));
            RectTransform nlRt = noiseLabel.GetComponent<RectTransform>();
            nlRt.anchorMin = new Vector2(0f, 0f);
            nlRt.anchorMax = new Vector2(1f, 1f);
            nlRt.pivot = new Vector2(0f, 0.5f);
            nlRt.anchoredPosition = new Vector2(48, 0);
            nlRt.sizeDelta = new Vector2(-54, 0);
            noiseLabel.alignment = TextAnchor.MiddleLeft;

            HUDPlayerVitals comp = vitalsGo.AddComponent<HUDPlayerVitals>();
            var so = new SerializedObject(comp);
            so.FindProperty("staminaBarFill").objectReferenceValue = stFill;
            so.FindProperty("staminaValueText").objectReferenceValue = stVal;
            so.FindProperty("statusBadgeBackground").objectReferenceValue = badgeBg;
            so.FindProperty("statusBadgeText").objectReferenceValue = badgeText;
            so.FindProperty("bleedoutContainer").objectReferenceValue = bleedContainer;
            so.FindProperty("bleedoutBarFill").objectReferenceValue = blFill;
            so.FindProperty("bleedoutTimerText").objectReferenceValue = bleedText;
            so.FindProperty("noiseIcon").objectReferenceValue = noiseIcon;
            so.FindProperty("noiseLabel").objectReferenceValue = noiseLabel;
            so.FindProperty("noiseCanvasGroup").objectReferenceValue = noiseCg;
            so.ApplyModifiedProperties();

            return comp;
        }

        private static HUDHotbar CreateHotbar(Transform parent)
        {
            GameObject hotbarGo = CreateUIObject("Hotbar_Panel", parent);
            RectTransform rt = hotbarGo.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(1f, 0f);
            rt.anchoredPosition = new Vector2(-40, 40);
            rt.sizeDelta = new Vector2(390, 126);

            Image bg = hotbarGo.AddComponent<Image>();
            bg.sprite = HUDTextureUtility.RoundedBox;
            bg.type = Image.Type.Sliced;
            bg.color = new Color(0.02f, 0.04f, 0.08f, 0.96f);

            Outline outline = hotbarGo.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0.85f, 1f, 0.45f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            // Slot 1
            GameObject s1Go = CreateSlotObject("Slot1", hotbarGo.transform, new Vector2(-262, 0), "1", out Image s1Icon, out Text s1Label, out GameObject s1Lock, out Text s1LockTxt);
            // Slot 2
            GameObject s2Go = CreateSlotObject("Slot2", hotbarGo.transform, new Vector2(-152, 0), "2", out Image s2Icon, out Text s2Label, out GameObject s2Lock, out Text s2LockTxt);
            // Team Tool Slot (Key 3 or T)
            GameObject toolGo = CreateToolSlotObject("ToolSlot", hotbarGo.transform, new Vector2(-42, 0), "3", out Image toolIcon, out Text toolLabel, out Image toolRadial, out Text toolCdText, out GameObject toolLock);

            HUDHotbar comp = hotbarGo.AddComponent<HUDHotbar>();
            var so = new SerializedObject(comp);
            so.FindProperty("slot1Container").objectReferenceValue = s1Go;
            so.FindProperty("slot1Icon").objectReferenceValue = s1Icon;
            so.FindProperty("slot1NameText").objectReferenceValue = s1Label;
            so.FindProperty("slot1LockOverlay").objectReferenceValue = s1Lock;
            so.FindProperty("slot1LockText").objectReferenceValue = s1LockTxt;

            so.FindProperty("slot2Container").objectReferenceValue = s2Go;
            so.FindProperty("slot2Icon").objectReferenceValue = s2Icon;
            so.FindProperty("slot2NameText").objectReferenceValue = s2Label;
            so.FindProperty("slot2LockOverlay").objectReferenceValue = s2Lock;
            so.FindProperty("slot2LockText").objectReferenceValue = s2LockTxt;

            so.FindProperty("toolContainer").objectReferenceValue = toolGo;
            so.FindProperty("toolIcon").objectReferenceValue = toolIcon;
            so.FindProperty("toolNameText").objectReferenceValue = toolLabel;
            so.FindProperty("toolCooldownRadial").objectReferenceValue = toolRadial;
            so.FindProperty("toolCooldownText").objectReferenceValue = toolCdText;
            so.FindProperty("toolLockedOverlay").objectReferenceValue = toolLock;
            so.ApplyModifiedProperties();

            return comp;
        }

        private static GameObject CreateSlotObject(string name, Transform parent, Vector2 anchoredPos, string keyNum,
            out Image slotIcon, out Text slotLabel, out GameObject lockOverlay, out Text lockText)
        {
            GameObject slotGo = CreateUIObject(name, parent);
            RectTransform rt = slotGo.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 0.5f);
            rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = new Vector2(92, 92);

            Image bg = slotGo.AddComponent<Image>();
            bg.sprite = HUDTextureUtility.RoundedBox;
            bg.type = Image.Type.Sliced;
            bg.color = new Color(0.06f, 0.12f, 0.2f, 0.95f);

            Outline slotOutline = slotGo.AddComponent<Outline>();
            slotOutline.effectColor = new Color(0f, 0.75f, 0.9f, 0.4f);
            slotOutline.effectDistance = new Vector2(1f, -1f);

            // Key tag
            Text key = CreateText("Key", slotGo.transform, keyNum, 15, FontStyle.Bold, new Color(0f, 0.9f, 1f, 1f));
            RectTransform kRt = key.GetComponent<RectTransform>();
            kRt.anchorMin = new Vector2(0f, 1f);
            kRt.anchorMax = new Vector2(0f, 1f);
            kRt.pivot = new Vector2(0f, 1f);
            kRt.anchoredPosition = new Vector2(6, -6);
            kRt.sizeDelta = new Vector2(24, 22);

            // Icon
            GameObject iconGo = CreateUIObject("Icon", slotGo.transform);
            RectTransform iRt = iconGo.GetComponent<RectTransform>();
            iRt.anchorMin = new Vector2(0.5f, 0.5f);
            iRt.anchorMax = new Vector2(0.5f, 0.5f);
            iRt.pivot = new Vector2(0.5f, 0.5f);
            iRt.anchoredPosition = new Vector2(0, 6);
            iRt.sizeDelta = new Vector2(46, 46);
            slotIcon = iconGo.AddComponent<Image>();
            slotIcon.sprite = HUDTextureUtility.CircleFilled;
            slotIcon.color = Color.white;
            slotIcon.gameObject.SetActive(false);

            // Label
            slotLabel = CreateText("Label", slotGo.transform, "Trống", 13, FontStyle.Bold, Color.white);
            RectTransform lRt = slotLabel.GetComponent<RectTransform>();
            lRt.anchorMin = new Vector2(0f, 0f);
            lRt.anchorMax = new Vector2(1f, 0f);
            lRt.pivot = new Vector2(0.5f, 0f);
            lRt.anchoredPosition = new Vector2(0, 6);
            lRt.sizeDelta = new Vector2(-6, 20);
            slotLabel.alignment = TextAnchor.MiddleCenter;

            // Lock Overlay
            lockOverlay = CreateUIObject("LockOverlay", slotGo.transform);
            RectTransform loRt = lockOverlay.GetComponent<RectTransform>();
            loRt.anchorMin = Vector2.zero;
            loRt.anchorMax = Vector2.one;
            loRt.sizeDelta = Vector2.zero;
            Image loImg = lockOverlay.AddComponent<Image>();
            loImg.sprite = HUDTextureUtility.RoundedBox;
            loImg.type = Image.Type.Sliced;
            loImg.color = new Color(0.9f, 0.3f, 0f, 0.75f);

            lockText = CreateText("LockText", lockOverlay.transform, "VÁC CORE", 12, FontStyle.Bold, Color.white);
            RectTransform ltRt = lockText.GetComponent<RectTransform>();
            ltRt.anchorMin = Vector2.zero;
            ltRt.anchorMax = Vector2.one;
            ltRt.sizeDelta = Vector2.zero;
            lockText.alignment = TextAnchor.MiddleCenter;

            lockOverlay.SetActive(false);
            return slotGo;
        }

        private static GameObject CreateToolSlotObject(string name, Transform parent, Vector2 anchoredPos, string keyNum,
            out Image toolIcon, out Text toolLabel, out Image toolRadial, out Text toolCdText, out GameObject toolLock)
        {
            GameObject slotGo = CreateUIObject(name, parent);
            RectTransform rt = slotGo.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 0.5f);
            rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = new Vector2(92, 92);

            Image bg = slotGo.AddComponent<Image>();
            bg.sprite = HUDTextureUtility.RoundedBox;
            bg.type = Image.Type.Sliced;
            bg.color = new Color(0.06f, 0.12f, 0.2f, 0.95f);

            Outline slotOutline = slotGo.AddComponent<Outline>();
            slotOutline.effectColor = new Color(0f, 0.75f, 0.9f, 0.4f);
            slotOutline.effectDistance = new Vector2(1f, -1f);

            // Key tag
            Text key = CreateText("Key", slotGo.transform, keyNum, 15, FontStyle.Bold, new Color(0f, 0.9f, 1f, 1f));
            RectTransform kRt = key.GetComponent<RectTransform>();
            kRt.anchorMin = new Vector2(0f, 1f);
            kRt.anchorMax = new Vector2(0f, 1f);
            kRt.pivot = new Vector2(0f, 1f);
            kRt.anchoredPosition = new Vector2(6, -6);
            kRt.sizeDelta = new Vector2(24, 22);

            // Tool Icon
            GameObject iconGo = CreateUIObject("Icon", slotGo.transform);
            RectTransform iRt = iconGo.GetComponent<RectTransform>();
            iRt.anchorMin = new Vector2(0.5f, 0.5f);
            iRt.anchorMax = new Vector2(0.5f, 0.5f);
            iRt.pivot = new Vector2(0.5f, 0.5f);
            iRt.anchoredPosition = new Vector2(0, 6);
            iRt.sizeDelta = new Vector2(46, 46);
            toolIcon = iconGo.AddComponent<Image>();
            toolIcon.sprite = HUDTextureUtility.CircleFilled;
            toolIcon.color = new Color(0.2f, 0.8f, 0.5f, 1f);
            toolIcon.gameObject.SetActive(false);

            // Cooldown Radial
            GameObject radialGo = CreateUIObject("CooldownRadial", slotGo.transform);
            RectTransform rRt = radialGo.GetComponent<RectTransform>();
            rRt.anchorMin = Vector2.zero;
            rRt.anchorMax = Vector2.one;
            rRt.sizeDelta = Vector2.zero;
            toolRadial = radialGo.AddComponent<Image>();
            toolRadial.sprite = HUDTextureUtility.CircleRing;
            toolRadial.type = Image.Type.Filled;
            toolRadial.fillMethod = Image.FillMethod.Radial360;
            toolRadial.fillOrigin = (int)Image.Origin360.Top;
            toolRadial.fillClockwise = false;
            toolRadial.fillAmount = 0f;
            toolRadial.color = new Color(0f, 0.9f, 1f, 0.9f);

            toolCdText = CreateText("CooldownText", radialGo.transform, "15s", 18, FontStyle.Bold, new Color(1f, 0.85f, 0.2f, 1f));
            RectTransform cdRt = toolCdText.GetComponent<RectTransform>();
            cdRt.anchorMin = Vector2.zero;
            cdRt.anchorMax = Vector2.one;
            cdRt.sizeDelta = Vector2.zero;
            toolCdText.alignment = TextAnchor.MiddleCenter;

            radialGo.SetActive(false);

            // Label
            toolLabel = CreateText("Label", slotGo.transform, "Tool", 13, FontStyle.Bold, Color.white);
            RectTransform lRt = toolLabel.GetComponent<RectTransform>();
            lRt.anchorMin = new Vector2(0f, 0f);
            lRt.anchorMax = new Vector2(1f, 0f);
            lRt.pivot = new Vector2(0.5f, 0f);
            lRt.anchoredPosition = new Vector2(0, 6);
            lRt.sizeDelta = new Vector2(-6, 20);
            toolLabel.alignment = TextAnchor.MiddleCenter;

            // Lock Overlay
            toolLock = CreateUIObject("LockOverlay", slotGo.transform);
            RectTransform tlRt = toolLock.GetComponent<RectTransform>();
            tlRt.anchorMin = Vector2.zero;
            tlRt.anchorMax = Vector2.one;
            tlRt.sizeDelta = Vector2.zero;
            Image tlImg = toolLock.AddComponent<Image>();
            tlImg.sprite = HUDTextureUtility.RoundedBox;
            tlImg.type = Image.Type.Sliced;
            tlImg.color = new Color(0.5f, 0.1f, 0.1f, 0.75f);
            toolLock.SetActive(false);

            return slotGo;
        }

        private static HUDTeammateStatus CreateTeammateStatus(Transform parent)
        {
            GameObject teamGo = CreateUIObject("TeammateStatus_Panel", parent);
            RectTransform rt = teamGo.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(30, -30);
            rt.sizeDelta = new Vector2(290, 270);

            CanvasGroup teamCg = teamGo.AddComponent<CanvasGroup>();
            teamCg.alpha = 0f;
            teamCg.interactable = false;
            teamCg.blocksRaycasts = false;

            Image bg = teamGo.AddComponent<Image>();
            bg.sprite = HUDTextureUtility.RoundedBox;
            bg.type = Image.Type.Sliced;
            bg.color = new Color(0.04f, 0.07f, 0.12f, 0.85f);

            // Header Title
            Text header = CreateText("HeaderTitle", teamGo.transform, "DANH SÁCH ĐỒNG ĐỘI", 12, FontStyle.Bold, new Color(0f, 0.9f, 1f, 1f));
            RectTransform hRt = header.GetComponent<RectTransform>();
            hRt.anchorMin = new Vector2(0f, 1f);
            hRt.anchorMax = new Vector2(1f, 1f);
            hRt.pivot = new Vector2(0.5f, 1f);
            hRt.anchoredPosition = new Vector2(0, -8);
            hRt.sizeDelta = new Vector2(-20, 20);
            header.alignment = TextAnchor.MiddleCenter;

            HUDTeammateStatus comp = teamGo.AddComponent<HUDTeammateStatus>();
            var so = new SerializedObject(comp);
            so.FindProperty("panelCanvasGroup").objectReferenceValue = teamCg;
            so.FindProperty("simulateTeammatesIfSolo").boolValue = false;
            var slotsProp = so.FindProperty("slots");

            for (int i = 0; i < 4; i++)
            {
                GameObject slotGo = CreateUIObject($"Slot_{i + 1}", teamGo.transform);
                RectTransform sRt = slotGo.GetComponent<RectTransform>();
                sRt.anchorMin = new Vector2(0f, 1f);
                sRt.anchorMax = new Vector2(1f, 1f);
                sRt.pivot = new Vector2(0.5f, 1f);
                sRt.anchoredPosition = new Vector2(0, -32 - (i * 56));
                sRt.sizeDelta = new Vector2(-16, 50);

                Image slotBg = slotGo.AddComponent<Image>();
                slotBg.sprite = HUDTextureUtility.RoundedBox;
                slotBg.color = new Color(0.08f, 0.12f, 0.18f, 0.9f);

                // Left Accent Bar
                GameObject accGo = CreateUIObject("Accent", slotGo.transform);
                RectTransform accRt = accGo.GetComponent<RectTransform>();
                accRt.anchorMin = new Vector2(0f, 0f);
                accRt.anchorMax = new Vector2(0f, 1f);
                accRt.pivot = new Vector2(0f, 0.5f);
                accRt.anchoredPosition = new Vector2(3, 0);
                accRt.sizeDelta = new Vector2(4, -6);
                Image accImg = accGo.AddComponent<Image>();
                accImg.sprite = HUDTextureUtility.WhitePixel;
                accImg.color = new Color(0f, 0.9f, 0.45f, 1f);

                // Name Text
                Text nameTxt = CreateText("Name", slotGo.transform, $"Player {i + 1}", 13, FontStyle.Bold, Color.white);
                RectTransform nRt = nameTxt.GetComponent<RectTransform>();
                nRt.anchorMin = new Vector2(0f, 1f);
                nRt.anchorMax = new Vector2(1f, 1f);
                nRt.pivot = new Vector2(0f, 1f);
                nRt.anchoredPosition = new Vector2(16, -6);
                nRt.sizeDelta = new Vector2(-100, 18);

                // Status Badge BG
                GameObject stBadgeGo = CreateUIObject("StatusBadge", slotGo.transform);
                RectTransform stbRt = stBadgeGo.GetComponent<RectTransform>();
                stbRt.anchorMin = new Vector2(1f, 1f);
                stbRt.anchorMax = new Vector2(1f, 1f);
                stbRt.pivot = new Vector2(1f, 1f);
                stbRt.anchoredPosition = new Vector2(-8, -6);
                stbRt.sizeDelta = new Vector2(85, 18);
                Image stBadgeBg = stBadgeGo.AddComponent<Image>();
                stBadgeBg.sprite = HUDTextureUtility.RoundedBox;
                stBadgeBg.color = new Color(0f, 0.9f, 0.45f, 0.2f);

                Text stTxt = CreateText("StatusText", stBadgeGo.transform, "KHỎE MẠNH", 10, FontStyle.Bold, new Color(0f, 0.9f, 0.45f, 1f));
                RectTransform sttRt = stTxt.GetComponent<RectTransform>();
                sttRt.anchorMin = Vector2.zero;
                sttRt.anchorMax = Vector2.one;
                sttRt.sizeDelta = Vector2.zero;
                stTxt.alignment = TextAnchor.MiddleCenter;

                // Core Carry Icon
                GameObject carryGo = CreateUIObject("CoreCarryIcon", slotGo.transform);
                RectTransform cRt = carryGo.GetComponent<RectTransform>();
                cRt.anchorMin = new Vector2(1f, 0f);
                cRt.anchorMax = new Vector2(1f, 0f);
                cRt.pivot = new Vector2(1f, 0f);
                cRt.anchoredPosition = new Vector2(-8, 6);
                cRt.sizeDelta = new Vector2(16, 16);
                Image carryImg = carryGo.AddComponent<Image>();
                carryImg.sprite = HUDTextureUtility.CircleFilled;
                carryImg.color = new Color(0f, 0.9f, 1f, 1f);
                carryGo.SetActive(false);

                // Distance Text
                Text distTxt = CreateText("Distance", slotGo.transform, "0m", 11, FontStyle.Normal, new Color(0.6f, 0.7f, 0.8f, 0.8f));
                RectTransform dRt = distTxt.GetComponent<RectTransform>();
                dRt.anchorMin = new Vector2(0f, 0f);
                dRt.anchorMax = new Vector2(0f, 0f);
                dRt.pivot = new Vector2(0f, 0f);
                dRt.anchoredPosition = new Vector2(16, 6);
                dRt.sizeDelta = new Vector2(50, 16);

                // HP Mini Bar
                GameObject hpBgGo = CreateUIObject("HPBarBG", slotGo.transform);
                RectTransform hpBgRt = hpBgGo.GetComponent<RectTransform>();
                hpBgRt.anchorMin = new Vector2(0f, 0f);
                hpBgRt.anchorMax = new Vector2(1f, 0f);
                hpBgRt.pivot = new Vector2(0.5f, 0f);
                hpBgRt.anchoredPosition = new Vector2(25, 12);
                hpBgRt.sizeDelta = new Vector2(-120, 4);
                Image hpBg = hpBgGo.AddComponent<Image>();
                hpBg.sprite = HUDTextureUtility.WhitePixel;
                hpBg.color = new Color(0.15f, 0.2f, 0.25f, 0.8f);

                GameObject hpFillGo = CreateUIObject("HPFill", hpBgGo.transform);
                RectTransform hpFillRt = hpFillGo.GetComponent<RectTransform>();
                hpFillRt.anchorMin = Vector2.zero;
                hpFillRt.anchorMax = Vector2.one;
                hpFillRt.sizeDelta = Vector2.zero;
                Image hpFill = hpFillGo.AddComponent<Image>();
                hpFill.sprite = HUDTextureUtility.WhitePixel;
                hpFill.type = Image.Type.Filled;
                hpFill.fillMethod = Image.FillMethod.Horizontal;
                hpFill.fillAmount = 1f;
                hpFill.color = new Color(0f, 0.9f, 0.45f, 1f);

                slotGo.SetActive(false);

                // Serialize into slot
                var element = slotsProp.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("root").objectReferenceValue = slotGo;
                element.FindPropertyRelative("background").objectReferenceValue = slotBg;
                element.FindPropertyRelative("accentBar").objectReferenceValue = accImg;
                element.FindPropertyRelative("nameText").objectReferenceValue = nameTxt;
                element.FindPropertyRelative("statusBadgeBg").objectReferenceValue = stBadgeBg;
                element.FindPropertyRelative("statusText").objectReferenceValue = stTxt;
                element.FindPropertyRelative("coreCarryIcon").objectReferenceValue = carryImg;
                element.FindPropertyRelative("healthFill").objectReferenceValue = hpFill;
                element.FindPropertyRelative("distanceText").objectReferenceValue = distTxt;
            }

            so.ApplyModifiedProperties();
            return comp;
        }

        private static HUD3DWorldMarker Create3DWorldMarkers(Transform parent)
        {
            GameObject markerRoot = CreateUIObject("WorldMarkers_Panel", parent);
            RectTransform rt = markerRoot.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;

            HUD3DWorldMarker comp = markerRoot.AddComponent<HUD3DWorldMarker>();
            var so = new SerializedObject(comp);
            var poolProp = so.FindProperty("markerPool");

            for (int i = 0; i < 4; i++)
            {
                GameObject mGo = CreateUIObject($"Marker_{i + 1}", markerRoot.transform);
                RectTransform mRt = mGo.GetComponent<RectTransform>();
                mRt.sizeDelta = new Vector2(150, 60);

                // Center Ring Pulse
                GameObject ringGo = CreateUIObject("PulseRing", mGo.transform);
                RectTransform ringRt = ringGo.GetComponent<RectTransform>();
                ringRt.anchorMin = new Vector2(0.5f, 0.5f);
                ringRt.anchorMax = new Vector2(0.5f, 0.5f);
                ringRt.pivot = new Vector2(0.5f, 0.5f);
                ringRt.sizeDelta = new Vector2(40, 40);
                Image ringImg = ringGo.AddComponent<Image>();
                ringImg.sprite = HUDTextureUtility.CircleRing;
                ringImg.color = new Color(1f, 0.25f, 0.25f, 0.8f);

                // Center Icon Dot
                GameObject iconGo = CreateUIObject("Icon", mGo.transform);
                RectTransform iconRt = iconGo.GetComponent<RectTransform>();
                iconRt.anchorMin = new Vector2(0.5f, 0.5f);
                iconRt.anchorMax = new Vector2(0.5f, 0.5f);
                iconRt.pivot = new Vector2(0.5f, 0.5f);
                iconRt.sizeDelta = new Vector2(16, 16);
                Image iconImg = iconGo.AddComponent<Image>();
                iconImg.sprite = HUDTextureUtility.CircleFilled;
                iconImg.color = new Color(1f, 0.25f, 0.25f, 1f);

                // Title label
                Text title = CreateText("Title", mGo.transform, "CỨU ĐỒNG ĐỘI", 12, FontStyle.Bold, new Color(1f, 0.25f, 0.25f, 1f));
                RectTransform tRt = title.GetComponent<RectTransform>();
                tRt.anchorMin = new Vector2(0.5f, 1f);
                tRt.anchorMax = new Vector2(0.5f, 1f);
                tRt.pivot = new Vector2(0.5f, 0f);
                tRt.anchoredPosition = new Vector2(0, 4);
                tRt.sizeDelta = new Vector2(180, 18);
                title.alignment = TextAnchor.MiddleCenter;

                // Distance label
                Text dist = CreateText("Distance", mGo.transform, "24m", 11, FontStyle.Bold, Color.white);
                RectTransform dRt = dist.GetComponent<RectTransform>();
                dRt.anchorMin = new Vector2(0.5f, 0f);
                dRt.anchorMax = new Vector2(0.5f, 0f);
                dRt.pivot = new Vector2(0.5f, 1f);
                dRt.anchoredPosition = new Vector2(0, -4);
                dRt.sizeDelta = new Vector2(80, 16);
                dist.alignment = TextAnchor.MiddleCenter;

                // Arrow Pointer
                GameObject arrowGo = CreateUIObject("ArrowPointer", mGo.transform);
                RectTransform aRt = arrowGo.GetComponent<RectTransform>();
                aRt.anchorMin = new Vector2(0.5f, 0.5f);
                aRt.anchorMax = new Vector2(0.5f, 0.5f);
                aRt.pivot = new Vector2(0.5f, 0.5f);
                aRt.anchoredPosition = new Vector2(0, 32);
                aRt.sizeDelta = new Vector2(20, 20);
                Image arrowImg = arrowGo.AddComponent<Image>();
                arrowImg.sprite = HUDTextureUtility.CircleFilled;
                arrowImg.color = new Color(1f, 0.25f, 0.25f, 1f);
                arrowGo.SetActive(false);

                mGo.SetActive(false);

                var element = poolProp.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("root").objectReferenceValue = mGo;
                element.FindPropertyRelative("rectTransform").objectReferenceValue = mRt;
                element.FindPropertyRelative("icon").objectReferenceValue = iconImg;
                element.FindPropertyRelative("pulseRing").objectReferenceValue = ringImg;
                element.FindPropertyRelative("titleText").objectReferenceValue = title;
                element.FindPropertyRelative("distanceText").objectReferenceValue = dist;
                element.FindPropertyRelative("arrowPointer").objectReferenceValue = arrowImg;
            }

            so.ApplyModifiedProperties();
            return comp;
        }

        private static GameObject CreateUIObject(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private static Text CreateText(string name, Transform parent, string content, int fontSize, FontStyle style, Color color)
        {
            GameObject go = CreateUIObject(name, parent);
            Text text = go.AddComponent<Text>();
            text.text = content;
            text.font = GetDefaultFont();
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.color = color;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;

            Shadow shadow = go.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.9f);
            shadow.effectDistance = new Vector2(1.5f, -1.5f);

            return text;
        }
    }
}
