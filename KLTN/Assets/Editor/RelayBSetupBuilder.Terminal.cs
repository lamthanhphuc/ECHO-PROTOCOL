using EchoProtocol.RelayB;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static partial class RelayBSetupBuilder
{
    [MenuItem("Tools/ECHO Protocol/Update Relay B Codebreaker UI")]
    public static void UpdateCodebreakerUi()
    {
        if (EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        var root = PrefabUtility.LoadPrefabContents(RelayBPrefabPath);
        try
        {
            var controller = root.GetComponent<RelayBController>();
            if (controller == null) throw new System.InvalidOperationException("Relay B controller is missing.");
            var ui = BuildUi(root.transform);
            var so = new SerializedObject(controller);
            SetObject(so, "ui", ui); so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, RelayBPrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        Debug.Log("[RelayB] Applied Containment / Decode / Sync terminal UI.");
    }

    private static readonly Color TerminalBack = new Color(0.045f, 0.052f, 0.058f, 1f);
    private static readonly Color TerminalLine = new Color(0.16f, 0.22f, 0.24f, 1f);
    private static readonly Color TerminalCyan = new Color(0.2f, 0.85f, 0.94f, 1f);
    private static readonly Color TerminalMuted = new Color(0.57f, 0.67f, 0.7f, 1f);

    private static RelayBUIController BuildUi(Transform prefabRoot)
    {
        var existing = prefabRoot.Find("RelayB_UI");
        var oldFont = existing != null ? existing.GetComponentInChildren<TMP_Text>(true)?.font : null;
        if (existing != null) Object.DestroyImmediate(existing.gameObject);
        var canvasRoot = new GameObject("RelayB_UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler),
            typeof(GraphicRaycaster), typeof(CanvasGroup), typeof(RelayBUIController));
        canvasRoot.transform.SetParent(prefabRoot, false);
        var canvas = canvasRoot.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 80;
        var scaler = canvasRoot.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 0.5f;
        Stretch(canvasRoot.GetComponent<RectTransform>());
        var frameSprite = LoadSprite("Assets/_Project/UI/BunkerSurvivalUI/Sprites/NineSlice/panel_industrial_main_normal_9slice.png");
        var panel = CreatePanel(canvasRoot.transform, "PanelRoot", frameSprite, new Color(0.7f, 0.73f, 0.75f));
        var panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = panelRect.anchorMax = panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(1160, 680); panelRect.anchoredPosition = Vector2.zero;
        panel.GetComponent<Image>().pixelsPerUnitMultiplier = 2f;
        TerminalSurface(panel.transform, "Matte", 12, 12, 1136, 656, TerminalBack);
        var stage = TerminalText(panel.transform, "StageLabel", "RELAY B / STAGE 01", 14, 40, 30, 360, 20, TerminalMuted);
        var title = TerminalText(panel.transform, "RelayLabel", "SURGE CONTAINMENT", 28, 40, 54, 560, 36, Color.white);
        var status = TerminalText(panel.transform, "StatusLabel", "AWAITING ACTIVATION", 16, 820, 56, 300, 32, TerminalMuted, TextAlignmentOptions.Right);
        TerminalSurface(panel.transform, "HeaderLine", 40, 104, 1080, 1, TerminalLine);
        var tabs = new Button[3]; var highlights = new Image[3];
        string[] labels = { "01  CONTAIN", "02  DECODE", "03  SYNC" };
        for (int i = 0; i < 3; i++)
        {
            tabs[i] = TerminalButton(panel.transform, "TabButton_" + i, labels[i], 40 + i * 180, 116, 164, 32, 14);
            highlights[i] = tabs[i].GetComponent<Image>();
        }
        var dots = new Image[5];
        for (int i = 0; i < 5; i++) dots[i] = TerminalSurface(panel.transform, "AttemptDot" + i, 916 + i * 38, 66, 16, 16, TerminalCyan).GetComponent<Image>();
        // Circular indicators use the existing Unity UI knob sprite.
        var knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        foreach (var dot in dots) { dot.sprite = knob; dot.raycastTarget = false; }
        var spectrum = TerminalContainer(panel.transform, "SpectrumTabPanel", 40, 172, 1080, 422);
        var processing = TerminalContainer(panel.transform, "ProcessingTabPanel", 40, 172, 1080, 422);
        var sync = TerminalContainer(panel.transform, "SyncTabPanel", 40, 172, 1080, 422);
        TerminalSurface(panel.transform, "FooterLine", 40, 610, 1080, 1, TerminalLine);
        var close = TerminalButton(panel.transform, "CloseButton", "CLOSE", 968, 628, 152, 36, 15);
        var reset = TerminalButton(panel.transform, "ResetInput", "RESET INPUT", 40, 628, 180, 36, 15);
        var start = TerminalButton(panel.transform, "StartLinkButton", "START SYNC", 40, 628, 200, 36, 15, true);
        var abort = TerminalButton(panel.transform, "AbortLinkButton", "CANCEL SYNC", 256, 628, 180, 36, 15);
        var ui = canvasRoot.GetComponent<RelayBUIController>(); var so = new SerializedObject(ui);
        SetObject(so, "panelRoot", panel); SetObject(so, "canvasGroup", canvasRoot.GetComponent<CanvasGroup>());
        SetObject(so, "relayLabel", title); SetObject(so, "stageLabel", stage); SetObject(so, "statusLabel", status);
        SetArray(so, "tabButtons", tabs); SetArray(so, "tabHighlights", highlights); SetArray(so, "attemptDots", dots);
        SetObject(so, "spectrumTabPanel", spectrum); SetObject(so, "processingTabPanel", processing); SetObject(so, "syncTabPanel", sync);
        SetObject(so, "closeButton", close); SetObject(so, "startLinkButton", start); SetObject(so, "abortLinkButton", abort);
        SetObject(so, "resetInputButton", reset);
        BuildContainmentTerminal(spectrum.transform, so);
        BuildDecodeTerminal(processing.transform, so);
        BuildSyncTerminal(sync.transform, so);
        so.ApplyModifiedPropertiesWithoutUndo();
        if (oldFont != null) foreach (var text in canvasRoot.GetComponentsInChildren<TMP_Text>(true)) text.font = oldFont;
        panel.SetActive(false);
        return ui;
    }

    private static void BuildContainmentTerminal(Transform parent, SerializedObject so)
    {
        // The seeded board is built by RelayBUIController at runtime. Keep a font reference
        // and an editor preview instead of recreating the retired channel-selection interface.
        var title = TerminalText(parent, "ContainmentTitle", "SURGE CONTAINMENT", 22, 0, 0, 600, 34, Color.white);
        var hint = TerminalText(parent, "ContainmentHint", "Insulate gray cells. Isolate every red surge before it reaches a green core.", 16, 0, 50, 940, 56, TerminalMuted);
        SetObject(so, "referenceProfileText", hint);
    }

    private static void BuildDecodeTerminal(Transform parent, SerializedObject so)
    {
        TerminalText(parent, "AnalyzerTitle", "SIGNAL ANALYZER", 15, 0, 0, 620, 22, TerminalMuted);
        var analyzer = TerminalWave(parent, "DecoderWave", 0, 30, 620, 100);
        TerminalSurface(parent, "Divider", 648, 0, 1, 422, TerminalLine);
        TerminalText(parent, "LogTitle", "TRANSMISSION LOG", 15, 678, 0, 400, 22, TerminalMuted);
        var slots = new Button[6]; var digits = new TMP_Text[6]; var feedback = new TMP_Text[6]; var edges = new Image[6];
        var codeIcons = new RelayBFeedbackGraphic[6]; var historyIcons = new RelayBFeedbackGraphic[30];
        for (int i = 0; i < 6; i++)
        {
            slots[i] = TerminalButton(parent, "CodeSlot_" + i, "", 42 + i * 88, 146, 76, 92, 34);
            var outline = slots[i].gameObject.AddComponent<Outline>();
            outline.effectDistance = new Vector2(1f, -1f); outline.useGraphicAlpha = false;
            feedback[i] = TerminalText(slots[i].transform, "Feedback", (i + 1).ToString("00"), 14, 8, 6, 60, 22, TerminalMuted);
            codeIcons[i] = TerminalFeedback(slots[i].transform, "ResultIcon", 10, 8, 18, RelayBCodeFeedback.Right, TerminalCyan);
            digits[i] = TerminalText(slots[i].transform, "Digit", "?", 34, 4, 30, 68, 44, Color.white, TextAlignmentOptions.Center);
            edges[i] = TerminalSurface(slots[i].transform, "Accent", 6, 86, 64, 3, TerminalLine).GetComponent<Image>();
        }
        var connection = TerminalSurface(parent, "DecodedConnection", 48, 242, 516, 3, new Color(0.35f, 1f, 0.58f)).GetComponent<Image>();
        connection.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
        connection.type = Image.Type.Filled; connection.fillMethod = Image.FillMethod.Horizontal;
        TerminalText(parent, "BankTitle", "SIGNAL BANK", 14, 0, 260, 620, 22, TerminalMuted, TextAlignmentOptions.Center);
        var bank = new Button[9];
        for (int i = 0; i < 9; i++)
        {
            bool lower = i >= 5; int col = lower ? i - 5 : i;
            bank[i] = TerminalButton(parent, "Signal_" + (i + 1), (i + 1).ToString(),
                (lower ? 182 : 146) + col * 68, lower ? 340 : 282, 56, 52, 24);
            bank[i].gameObject.AddComponent<CanvasGroup>();
        }
        var transmit = TerminalButton(parent, "Transmit", "TRANSMIT", 204, 400, 208, 42, 16, true);
        // Main content ends above the footer; compact bank leaves a dedicated command row.
        SetAnchored(transmit.gameObject, new Vector2(204, -396), new Vector2(208, 30), new Vector2(0, 1));
        var notice = TerminalText(parent, "DecodeNotice", "6 SIGNALS / NO DUPLICATES", 13, 678, 342, 400, 24, TerminalMuted);
        TerminalFeedback(parent, "LegendRightIcon", 678, 384, 16, RelayBCodeFeedback.Right, new Color(0.35f, 1f, 0.58f));
        TerminalText(parent, "LegendRight", "RIGHT", 13, 702, 382, 88, 22, new Color(0.35f, 1f, 0.58f));
        TerminalFeedback(parent, "LegendPlaceIcon", 814, 384, 16, RelayBCodeFeedback.WrongPlace, new Color(1f, 0.78f, 0.18f));
        TerminalText(parent, "LegendPlace", "PLACE", 13, 838, 382, 88, 22, new Color(1f, 0.78f, 0.18f));
        TerminalFeedback(parent, "LegendUnusedIcon", 944, 384, 16, RelayBCodeFeedback.Unused, TerminalMuted);
        TerminalText(parent, "LegendUnused", "UNUSED", 13, 968, 382, 110, 22, TerminalMuted);
        var logDigits = new TMP_Text[30]; var logFeedback = new TMP_Text[30]; var cells = new Image[30];
        for (int row = 0; row < 5; row++)
        {
            TerminalText(parent, "Attempt_" + row, (row + 1).ToString("00"), 14, 678, 48 + row * 56, 32, 42, TerminalMuted);
            for (int col = 0; col < 6; col++)
            {
                int index = row * 6 + col;
                var cell = TerminalSurface(parent, "History_" + index, 718 + col * 60, 42 + row * 56, 44, 46, TerminalLine);
                cells[index] = cell.GetComponent<Image>();
                logDigits[index] = TerminalText(cell.transform, "Digit", "-", 18, 2, 2, 40, 24, TerminalMuted, TextAlignmentOptions.Center);
                logFeedback[index] = TerminalText(cell.transform, "Feedback", "", 14, 2, 26, 40, 18, TerminalMuted, TextAlignmentOptions.Center);
                historyIcons[index] = TerminalFeedback(cell.transform, "ResultIcon", 14, 28, 16, RelayBCodeFeedback.Unused, TerminalMuted);
            }
        }
        var next = TerminalButton(parent, "DecodeContinue", "SYNC SIGNAL  >", 204, 396, 208, 30, 16, true);
        SetArray(so, "codeSlots", slots); SetArray(so, "codeDigits", digits); SetArray(so, "codeFeedback", feedback);
        SetArray(so, "codeEdges", edges); SetArray(so, "signalBank", bank);
        SetArray(so, "codeIcons", codeIcons); SetArray(so, "historyIcons", historyIcons);
        SetArray(so, "historyDigits", logDigits); SetArray(so, "historyFeedback", logFeedback); SetArray(so, "historyCells", cells);
        SetObject(so, "decodeAnalyzer", analyzer); SetObject(so, "decodeConnection", connection);
        SetObject(so, "transmitButton", transmit); SetObject(so, "decodeNotice", notice); SetObject(so, "decodeContinue", next);
    }

    private static void BuildSyncTerminal(Transform parent, SerializedObject so)
    {
        TerminalText(parent, "ScopeTitle", "CARRIER ALIGNMENT", 15, 0, 0, 550, 24, TerminalMuted);
        var referenceLabel = TerminalText(parent, "ReferenceLabel", "REFERENCE", 14, 0, 38, 540, 22, TerminalCyan);
        var reference = TerminalWave(parent, "ReferenceWave", 0, 68, 550, 134);
        var currentLabel = TerminalText(parent, "CurrentLabel", "CURRENT", 14, 0, 226, 540, 22, TerminalMuted);
        var current = TerminalWave(parent, "CurrentWave", 0, 256, 550, 134);
        TerminalSurface(parent, "Divider", 578, 0, 1, 418, TerminalLine);
        var frequencyText = TerminalText(parent, "FrequencyValueText", "FREQUENCY", 16, 610, 16, 468, 26, Color.white);
        var frequency = CreateSlider(parent, "FrequencySlider", new Vector2(610, -64), new Vector2(468, 26), 10, 100, 50);
        var phaseText = TerminalText(parent, "PhaseValueText", "PHASE", 16, 610, 132, 468, 26, Color.white);
        var phase = CreateSlider(parent, "PhaseSlider", new Vector2(610, -180), new Vector2(468, 26), 0, 360, 0);
        var progressText = TerminalText(parent, "LinkProgressText", "SYNC", 18, 610, 260, 468, 28, Color.white);
        var progress = CreateBar(parent, "Progress", new Vector2(610, -308), new Vector2(468, 12), new Color(0.35f, 1f, 0.58f));
        var notice = TerminalText(parent, "WarningBanner", "ALIGN CARRIERS", 16, 610, 350, 468, 60, TerminalMuted);
        SetObject(so, "referenceWaveformRenderer", reference); SetObject(so, "currentWaveformRenderer", current);
        SetObject(so, "referenceSignalLabel", referenceLabel); SetObject(so, "currentSignalLabel", currentLabel);
        SetObject(so, "frequencyValueText", frequencyText); SetObject(so, "phaseValueText", phaseText);
        SetObject(so, "frequencySlider", frequency); SetObject(so, "phaseSlider", phase);
        SetObject(so, "linkProgressText", progressText); SetObject(so, "linkProgressFill", progress); SetObject(so, "warningBannerText", notice);
    }

    private static GameObject TerminalContainer(Transform parent, string name, float x, float y, float w, float h)
    {
        var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
        SetAnchored(go, new Vector2(x, -y), new Vector2(w, h), new Vector2(0, 1)); return go;
    }
    private static GameObject TerminalSurface(Transform parent, string name, float x, float y, float w, float h, Color color)
    {
        var go = CreatePanel(parent, name, null, color);
        SetAnchored(go, new Vector2(x, -y), new Vector2(w, h), new Vector2(0, 1));
        go.GetComponent<Image>().raycastTarget = false; return go;
    }
    private static TMP_Text TerminalText(Transform parent, string name, string text, float font, float x, float y, float w, float h,
        Color color, TextAlignmentOptions align = TextAlignmentOptions.Left)
    {
        var tmp = CreateText(parent, name, text, font, new Vector2(x, -y), new Vector2(w, h), align);
        tmp.color = color; return tmp;
    }
    private static Button TerminalButton(Transform parent, string name, string text, float x, float y, float w, float h, float font, bool primary = false)
    {
        var button = CreateButton(parent, name, text, null, new Vector2(x, -y), new Vector2(w, h), font);
        button.GetComponent<Image>().color = primary ? new Color(0.075f, 0.25f, 0.28f) : new Color(0.075f, 0.1f, 0.115f);
        var colors = button.colors; colors.disabledColor = new Color(0.35f, 0.35f, 0.35f, 1f);
        colors.highlightedColor = new Color(1.2f, 1.35f, 1.4f, 1f); colors.fadeDuration = 0.08f; button.colors = colors;
        TerminalSurface(button.transform, "BottomLine", 0, h - 1, w, 1, TerminalLine);
        return button;
    }
    private static RelayBWaveformRenderer TerminalWave(Transform parent, string name, float x, float y, float w, float h)
    {
        var go = TerminalContainer(parent, name, x, y, w, h);
        var wave = go.AddComponent<RelayBWaveformRenderer>(); wave.raycastTarget = false; return wave;
    }
    private static RelayBFeedbackGraphic TerminalFeedback(Transform parent, string name, float x, float y, float size, RelayBCodeFeedback feedback, Color color)
    {
        var go = TerminalContainer(parent, name, x, y, size, size);
        var graphic = go.AddComponent<RelayBFeedbackGraphic>(); graphic.SetFeedback(feedback, color); return graphic;
    }
}
